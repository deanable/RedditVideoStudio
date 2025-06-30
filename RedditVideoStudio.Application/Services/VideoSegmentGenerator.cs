// In: C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Application\Services\VideoSegmentGenerator.cs

using Microsoft.Extensions.Logging;
using RedditVideoStudio.Application.Extensions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Shared.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Application.Services
{
    public class VideoSegmentGenerator : IVideoSegmentGenerator
    {
        private readonly IPexelsService _pexelsService;
        private readonly IFfmpegService _ffmpegService;
        private readonly IAudioUtility _audioUtility;
        private readonly ILogger<VideoSegmentGenerator> _logger;

        public VideoSegmentGenerator(
            IPexelsService pexelsService,
            IFfmpegService ffmpegService,
            IAudioUtility audioUtility,
            ILogger<VideoSegmentGenerator> logger)
        {
            _pexelsService = pexelsService;
            _ffmpegService = ffmpegService;
            _audioUtility = audioUtility;
            _logger = logger;
        }

        public async Task<string> GenerateAsync(
            Storyboard storyboard,
            string backgroundQuery,
            string tempPath,
            string segmentName,
            string outputSegmentPath,
            IProgress<ProgressReport> progress,
            CancellationToken cancellationToken)
        {
            progress.Report(new ProgressReport { Message = $"Downloading background for {segmentName}..." });
            string backgroundPath = Path.Combine(tempPath, $"{segmentName}_bg.mp4");
            await _pexelsService.DownloadRandomVideoAsync(backgroundQuery, backgroundPath, cancellationToken);

            TimeSpan videoDuration;
            string? narrationPath = null;

            if (storyboard.Items.Any(item => !string.IsNullOrEmpty(item.AudioPath)))
            {
                var audioPaths = storyboard.Items
                    .Where(i => !string.IsNullOrWhiteSpace(i.AudioPath))
                    .Select(i => i.AudioPath!)
                    .ToList();

                // --- START OF FINAL CORRECTION ---

                if (audioPaths.Count == 1)
                {
                    // If there's only one audio file, skip concatenation and use the source directly.
                    narrationPath = audioPaths.First();
                    _logger.LogInformation("Single audio file found for segment '{segmentName}'. Using source directly: {Path}", segmentName, narrationPath);
                }
                else
                {
                    // If there are multiple files, they must be concatenated.
                    progress.Report(new ProgressReport { Message = $"Combining {audioPaths.Count} audio parts for {segmentName}..." });
                    narrationPath = Path.Combine(tempPath, $"{segmentName}_narration.mp3");
                    await storyboard.SaveConcatenatedAudioAsync(narrationPath, _ffmpegService, cancellationToken);
                    _logger.LogInformation("{Count} audio files for segment '{segmentName}' concatenated into: {Path}", audioPaths.Count, segmentName, narrationPath);
                }

                // Get the single source of truth for duration from the FINAL audio asset.
                videoDuration = await _audioUtility.GetAudioDurationAsync(narrationPath!, cancellationToken);
                _logger.LogInformation("Actual final audio duration for segment '{segmentName}' is {Duration}", segmentName, videoDuration);

                // Rescale the storyboard item timings to perfectly match the final audio duration.
                var storyboardTotalDuration = storyboard.GetNextStartTime();
                if (storyboardTotalDuration.TotalMilliseconds > 0)
                {
                    double scaleFactor = videoDuration.TotalMilliseconds / storyboardTotalDuration.TotalMilliseconds;
                    if (Math.Abs(scaleFactor - 1.0) > 0.01) // Only rescale if there's a meaningful difference (e.g., > 1%)
                    {
                        _logger.LogWarning("Rescaling storyboard timings for segment '{segmentName}' by a factor of {ScaleFactor}", segmentName, scaleFactor);
                        TimeSpan cumulativeDuration = TimeSpan.Zero;
                        foreach (var item in storyboard.Items)
                        {
                            TimeSpan originalDuration = item.EndTime - item.StartTime;
                            TimeSpan scaledDuration = TimeSpan.FromMilliseconds(originalDuration.TotalMilliseconds * scaleFactor);
                            item.StartTime = cumulativeDuration;
                            item.EndTime = cumulativeDuration + scaledDuration;
                            cumulativeDuration = item.EndTime;
                        }
                    }
                }
                // --- END OF FINAL CORRECTION ---
            }
            else
            {
                videoDuration = storyboard.GetNextStartTime();
                _logger.LogWarning("No audio items found for segment {SegmentName}, using storyboard duration: {Duration}", segmentName, videoDuration);
            }

            progress.Report(new ProgressReport { Message = $"Rendering video for {segmentName}..." });

            _logger.LogInformation("Passing duration {Duration} to RenderFinalVideoAsync for segment '\"{SegmentName}\"'.", videoDuration, segmentName);
            await _ffmpegService.RenderFinalVideoAsync(backgroundPath, narrationPath, storyboard, videoDuration, progress, outputSegmentPath, cancellationToken);

            _logger.LogInformation("Successfully generated video segment '\"{SegmentName}\"' at \"{Path}\"", segmentName, outputSegmentPath);
            return outputSegmentPath;
        }
    }
}