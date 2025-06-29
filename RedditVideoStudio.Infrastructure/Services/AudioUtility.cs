using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class AudioUtility : IAudioUtility
    {
        private readonly IAppConfiguration _appConfig;
        private readonly ILogger<AudioUtility> _logger;

        public AudioUtility(IAppConfiguration appConfig, ILogger<AudioUtility> logger)
        {
            _appConfig = appConfig;
            _logger = logger;
        }

        public async Task<TimeSpan> GetAudioDurationAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(audioFilePath))
            {
                _logger.LogError("Audio file not found at {Path}", audioFilePath);
                throw new FileNotFoundException("Audio file not found.", audioFilePath);
            }

            // ADDED: Logging for debugging
            _logger.LogInformation("Getting audio duration for: \"{Path}\"", audioFilePath);

            var ffprobePath = _appConfig.Settings.Ffmpeg.FfprobeExePath;

            if (!File.Exists(ffprobePath))
            {
                throw new FileNotFoundException("ffprobe.exe not found. Please check your FFmpeg installation path in settings.", ffprobePath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new FfmpegException("Failed to start ffprobe process.");
                }

                string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                string error = await process.StandardError.ReadToEndAsync(cancellationToken);

                // ADDED: Logging for debugging
                _logger.LogInformation("ffprobe raw output: '{Output}'", output.Trim());

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    throw new FfmpegException($"ffprobe process exited with code {process.ExitCode}. Error: {error}", process.ExitCode, error, null);
                }

                if (double.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var seconds))
                {
                    var duration = TimeSpan.FromSeconds(seconds);
                    // ADDED: Logging for debugging
                    _logger.LogInformation("Parsed duration: {Duration}", duration);
                    return duration;
                }

                throw new FormatException($"Unable to parse duration from ffprobe output. Output: '{output}'. Error: '{error}'");
            }
            catch (Exception ex) when (ex is not FfmpegException && ex is not FormatException)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting audio duration for {File}", audioFilePath);
                throw new FfmpegException("An unexpected error occurred while running ffprobe.", ex);
            }
        }
    }
}