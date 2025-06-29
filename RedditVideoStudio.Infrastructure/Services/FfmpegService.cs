// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\FfmpegService.cs

using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class FfmpegService : IFfmpegService
    {
        private readonly IAppConfiguration _appConfig;
        private readonly ILogger<FfmpegService> _logger;

        public FfmpegService(IAppConfiguration appConfig, ILogger<FfmpegService> logger)
        {
            _appConfig = appConfig;
            _logger = logger;
        }

        public async Task ConcatenateAudioAsync(IEnumerable<string> audioPaths, string outputPath, CancellationToken cancellationToken = default)
        {
            var validPaths = audioPaths.Where(File.Exists).ToList();
            if (!validPaths.Any())
            {
                throw new InvalidOperationException("No valid audio files to concatenate.");
            }

            var tempListPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_audio_concat_{Guid.NewGuid()}.txt");

            var fileContent = string.Join(Environment.NewLine, validPaths.Select(p => $"file '{p.Replace("'", "'\\''")}'"));
            await File.WriteAllTextAsync(tempListPath, fileContent, cancellationToken);

            // CORRECTED: Replaced '-c copy' with an explicit audio codec and bitrate.
            // This forces FFmpeg to re-encode the audio, which is necessary when the
            // input format (e.g., WAV from Windows TTS) differs from the output (MP3).
            var ffmpegSettings = _appConfig.Settings.Ffmpeg;
            var args = $"-f concat -safe 0 -i \"{tempListPath}\" -c:a {ffmpegSettings.AudioCodec} -b:a {ffmpegSettings.AudioBitrate} \"{outputPath}\" -y";

            try
            {
                await RunFfmpegAsync(args, null, TimeSpan.Zero, cancellationToken);
                _logger.LogInformation("Audio concatenation successful: {OutputPath}", outputPath);
            }
            finally
            {
                if (File.Exists(tempListPath))
                {
                    File.Delete(tempListPath);
                }
            }
        }

        public async Task<bool> HasAudioStreamAsync(string videoPath, CancellationToken cancellationToken)
        {
            if (!File.Exists(videoPath))
            {
                _logger.LogWarning("Video file not found for audio stream check: {Path}", videoPath);
                return false;
            }

            string ffprobePath = _appConfig.Settings.Ffmpeg.FfprobeExePath;
            var arguments = $"-v quiet -print_format json -show_streams -select_streams a \"{videoPath}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new FfmpegException("Failed to start ffprobe process to check for audio stream.");
            }

            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("ffprobe failed to check audio stream for {Path}. Error: {Error}", videoPath, error);
                return false;
            }

            using var jsonDoc = JsonDocument.Parse(output);
            return jsonDoc.RootElement.TryGetProperty("streams", out var streams) && streams.GetArrayLength() > 0;
        }

        public async Task<string> CreateSilentAudioAsync(TimeSpan duration, string outputPath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating silent audio track with duration {Duration}s.", duration.TotalSeconds);

            var args = $"-f lavfi -i anullsrc=r=44100:cl=stereo -t {duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} -q:a 9 -acodec libmp3lame \"{outputPath}\" -y";

            await RunFfmpegAsync(args, null, TimeSpan.Zero, cancellationToken);

            _logger.LogInformation("Silent audio track saved to: {OutputPath}", outputPath);
            return outputPath;
        }

        public async Task<TimeSpan> GetVideoDurationAsync(string videoFilePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(videoFilePath))
                throw new FileNotFoundException("Video file not found.", videoFilePath);

            string ffprobePath = _appConfig.Settings.Ffmpeg.FfprobeExePath;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoFilePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start ffprobe process.");
                string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (double.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }
                throw new FormatException($"Unable to parse duration from ffprobe output for {videoFilePath}: '{output}'");
            }
            catch (Exception ex)
            {
                throw new FfmpegException($"Failed to get video duration for {videoFilePath}.", ex);
            }
        }

        public async Task ConcatenateVideosAsync(IEnumerable<string> videoPaths, string outputPath, CancellationToken cancellationToken = default)
        {
            var validPaths = videoPaths.Where(File.Exists).ToList();
            if (validPaths.Count < 1)
            {
                _logger.LogWarning("No valid video files provided for concatenation.");
                return;
            }

            var tempListPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_concat_list_{Guid.NewGuid()}.txt");
            var fileContent = string.Join(Environment.NewLine, validPaths.Select(p => $"file '{p.Replace("'", "'\\''")}'"));

            await File.WriteAllTextAsync(tempListPath, fileContent, cancellationToken);

            var args = $"-f concat -safe 0 -i \"{tempListPath}\" -c copy \"{outputPath}\" -y";
            await RunFfmpegAsync(args, null, TimeSpan.Zero, cancellationToken);

            File.Delete(tempListPath);
            _logger.LogInformation("Video concatenation successful: {OutputPath}", outputPath);
        }

        public async Task<string> TrimVideoAsync(string inputPath, TimeSpan duration, string outputPath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Trimming video {Input} to {Duration}s.", inputPath, duration.TotalSeconds);
            var ffmpegSettings = _appConfig.Settings.Ffmpeg;

            // --- START OF CORRECTION ---
            //
            // Replaced "-c copy" with a full re-encode using project settings.
            // This is slower but guarantees the output duration is precise, fixing the sync drift.
            //
            var args = $"-i \"{inputPath}\" -t {duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} -c:v {ffmpegSettings.VideoCodec} -preset {ffmpegSettings.Preset} -b:v {ffmpegSettings.VideoBitrate} -c:a {ffmpegSettings.AudioCodec} -b:a {ffmpegSettings.AudioBitrate} \"{outputPath}\" -y";
            //
            // --- END OF CORRECTION ---

            await RunFfmpegAsync(args, null, TimeSpan.Zero, cancellationToken);

            _logger.LogInformation("Trimmed video saved to: {OutputPath}", outputPath);
            return outputPath;
        }

        public async Task<string> NormalizeVideoAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Normalizing video '{Input}' to standard format at '{Output}'.", inputPath, outputPath);
            var ffmpegSettings = _appConfig.Settings.Ffmpeg;
            bool hasAudio = await HasAudioStreamAsync(inputPath, cancellationToken);

            var arguments = new StringBuilder();
            arguments.Append($"-i \"{inputPath}\" ");

            if (!hasAudio)
            {
                // If the input video has no audio, we must generate a silent track to make it compatible
                // with other clips that do have audio. '-f lavfi' creates a virtual input device.
                _logger.LogWarning("Video '{Input}' has no audio. Adding silent track during normalization.", inputPath);
                var duration = await GetVideoDurationAsync(inputPath, cancellationToken);
                arguments.Append($"-f lavfi -i anullsrc=r=44100:cl=stereo -t {duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
            }

            // Standardize video stream: codec, preset, bitrate, resolution, and pixel format (yuv420p is widely compatible).
            arguments.Append($"-c:v {ffmpegSettings.VideoCodec} -preset {ffmpegSettings.Preset} -b:v {ffmpegSettings.VideoBitrate} -s 1920x1080 -pix_fmt yuv420p ");

            // Standardize audio stream: codec and bitrate.
            arguments.Append($"-c:a {ffmpegSettings.AudioCodec} -b:a {ffmpegSettings.AudioBitrate} ");

            // Map the correct streams to the output.
            arguments.Append("-map 0:v:0 "); // Map video from the first input (the clip file).
            if (!hasAudio)
            {
                arguments.Append("-map 1:a:0 "); // If we added a silent track, map it from the second input.
            }
            else
            {
                arguments.Append("-map 0:a:0 "); // Otherwise, map the original audio from the first input.
            }

            // Ensure the output duration is capped by the shortest stream (important when adding silent audio).
            arguments.Append($"-shortest -y \"{outputPath}\"");

            await RunFfmpegAsync(arguments.ToString(), null, TimeSpan.Zero, cancellationToken);

            _logger.LogInformation("Successfully normalized video to {OutputPath}", outputPath);
            return outputPath;
        }


        public async Task<string> MergeAudioAndVideoAsync(string videoPath, string audioPath, string outputPath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Merging video '{VideoPath}' and audio '{AudioPath}' without re-encoding video.", videoPath, audioPath);

            var args = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac -shortest \"{outputPath}\" -y";

            await RunFfmpegAsync(args, null, TimeSpan.Zero, cancellationToken);

            _logger.LogInformation("Successfully merged media to {OutputPath}", outputPath);
            return outputPath;
        }

        public async Task RenderFinalVideoAsync(string backgroundVideoPath, string? narrationAudioPath, Storyboard storyboard, TimeSpan videoDuration, IProgress<ProgressReport> progress, string outputPath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("RenderFinalVideoAsync received request to render video with duration: {Duration}", videoDuration);

            var inputArgs = new List<string>
            {
                $"-stream_loop -1 -i \"{backgroundVideoPath}\""
            };

            if (!string.IsNullOrEmpty(narrationAudioPath) && File.Exists(narrationAudioPath))
            {
                inputArgs.Add($"-i \"{narrationAudioPath}\"");
            }

            foreach (var item in storyboard.Items)
            {
                if (File.Exists(item.ImagePath))
                {
                    inputArgs.Add($"-i \"{item.ImagePath}\"");
                }
            }

            var filterComplex = new StringBuilder("[0:v]scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:-1:-1:color=black[bg];");
            var lastVideoStream = "[bg]";
            var overlayInputIndex = string.IsNullOrEmpty(narrationAudioPath) ? 1 : 2;

            for (int i = 0; i < storyboard.Items.Count; i++)
            {
                var item = storyboard.Items[i];
                var currentOutput = $"[v{i + 1}]";
                var position = GetOverlayPosition(item.Position);
                var enable = $"between(t,{item.StartTime.TotalSeconds.ToString(CultureInfo.InvariantCulture)},{item.EndTime.TotalSeconds.ToString(CultureInfo.InvariantCulture)})";

                filterComplex.Append($"{lastVideoStream}[{overlayInputIndex}:v]overlay={position}:enable='{enable}'{(i == storyboard.Items.Count - 1 ? "[v_out]" : currentOutput)}");
                if (i < storyboard.Items.Count - 1)
                {
                    filterComplex.Append(';');
                }
                lastVideoStream = currentOutput;
                overlayInputIndex++;
            }

            var ffmpegSettings = _appConfig.Settings.Ffmpeg;
            var finalVideoMap = storyboard.Items.Any() ? "[v_out]" : "[bg]";
            var totalDuration = videoDuration; // Use the precise duration passed into the method

            var arguments = new StringBuilder();
            arguments.Append(string.Join(" ", inputArgs));
            arguments.Append(" -hide_banner");
            arguments.Append($" -filter_complex \"{filterComplex.ToString().TrimEnd(';')}\"");
            arguments.Append($" -map \"{finalVideoMap}\"");

            if (!string.IsNullOrEmpty(narrationAudioPath) && File.Exists(narrationAudioPath))
            {
                arguments.Append(" -map 1:a");
            }

            arguments.Append($" -t {totalDuration.TotalSeconds.ToString(CultureInfo.InvariantCulture)}");
            arguments.Append($" -c:v {ffmpegSettings.VideoCodec} -preset {ffmpegSettings.Preset} -b:v {ffmpegSettings.VideoBitrate}");
            arguments.Append($" -c:a {ffmpegSettings.AudioCodec} -b:a {ffmpegSettings.AudioBitrate}");
            arguments.Append($" -y \"{outputPath}\"");

            await RunFfmpegAsync(arguments.ToString(), progress, totalDuration, cancellationToken);
            _logger.LogInformation("Video rendering complete: {Output}", outputPath);
        }

        private string GetOverlayPosition(string position) => position.ToLowerInvariant() switch
        {
            "top-left" => "x=0:y=0",
            "top-right" => "x=W-w:y=0",
            "bottom-left" => "x=0:y=H-h",
            "bottom-right" => "x=W-w:y=H-h",
            "center" => "x=(W-w)/2:y=(H-h)/2",
            _ => "x=(W-w)/2:y=(H-h)/2"
        };

        private async Task RunFfmpegAsync(string arguments, IProgress<ProgressReport>? progress, TimeSpan totalDuration, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _appConfig.Settings.Ffmpeg.FfmpegExePath,
                Arguments = arguments,
                WorkingDirectory = AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var errorOutput = new StringBuilder();

            process.ErrorDataReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data)) return;
                _logger.LogTrace("FFmpeg: {Data}", args.Data);
                errorOutput.AppendLine(args.Data);
                if (progress != null && totalDuration > TimeSpan.Zero)
                {
                    var match = Regex.Match(args.Data, @"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})");
                    if (match.Success)
                    {
                        var currentTime = new TimeSpan(0, int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value) * 10);
                        var percentage = (int)((currentTime.TotalSeconds / totalDuration.TotalSeconds) * 100);
                        progress.Report(new ProgressReport { Percentage = Math.Min(100, percentage), Message = $"Rendering video... {percentage}%" });
                    }
                }
            };

            try
            {
                _logger.LogInformation("Starting FFmpeg with arguments: {Arguments}", arguments);

                // CORRECTED: Check the return value of process.Start()
                if (!process.Start())
                {
                    // If the process fails to start, throw a specific exception.
                    throw new FfmpegException($"Failed to start FFmpeg process. Ensure '{startInfo.FileName}' exists and is accessible.");
                }

                process.BeginErrorReadLine();
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    throw new FfmpegException($"FFmpeg process exited with code {process.ExitCode}.", process.ExitCode, errorOutput.ToString(), null);
                }

                _logger.LogInformation("FFmpeg process completed successfully.");
            }
            catch (Exception ex) when (ex is not FfmpegException)
            {
                _logger.LogError(ex, "An unexpected error occurred while running FFmpeg.");
                throw new FfmpegException("An unexpected error occurred while running FFmpeg.", ex);
            }
        }
        
        public async Task<string> ConvertWavToMp3Async(string inputWavPath, string outputMp3Path, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Converting '{Input}' to MP3 at '{Output}'.", inputWavPath, outputMp3Path);

            var args = $"-i \"{inputWavPath}\" -acodec libmp3lame -q:a 2 \"{outputMp3Path}\" -y";

            await RunFfmpegAsync(args, null, TimeSpan.Zero, cancellationToken);

            _logger.LogInformation("Successfully converted WAV to MP3.", outputMp3Path);
            return outputMp3Path;
        }
    }
}