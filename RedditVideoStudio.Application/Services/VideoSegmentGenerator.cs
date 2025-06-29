// In C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Application\Services\VideoSegmentGenerator.cs

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

            // --- START OF CORRECTION ---

            if (storyboard.Items.Any() && storyboard.Items.Any(item => !string.IsNullOrEmpty(item.AudioPath)))
            {
                // This block executes when there is audio to process.
                progress.Report(new ProgressReport { Message = $"Combining audio for {segmentName}..." });
                narrationPath = Path.Combine(tempPath, $"{segmentName}_narration.mp3");
                await storyboard.SaveConcatenatedAudioAsync(narrationPath, _ffmpegService, cancellationToken);

                // Re-measure the duration from the FINAL concatenated audio file. This is now the single source of truth.
                videoDuration = await _audioUtility.GetAudioDurationAsync(narrationPath, cancellationToken);
                _logger.LogInformation("Actual duration of concatenated narration track '\"{NarrationPath}\"' is {Duration}", narrationPath, videoDuration);

                // IMPORTANT: Update the storyboard's timing information with the new, accurate duration.
                // This ensures the overlay timings passed to FFmpeg match the actual audio.
                if (storyboard.Items.Any())
                {
                    // For single-page segments, this makes the overlay last the full, correct duration.
                    storyboard.Items.Last().EndTime = videoDuration;
                }
            }
            else
            {
                // This block executes if there's no audio, using the storyboard's pre-calculated duration.
                videoDuration = storyboard.GetNextStartTime();
                _logger.LogWarning("No audio items found for segment {SegmentName}, using storyboard duration: {Duration}", segmentName, videoDuration);
            }

            // --- END OF CORRECTION ---

            progress.Report(new ProgressReport { Message = $"Rendering video for {segmentName}..." });

            // This log will now show the correct, re-measured duration.
            _logger.LogInformation("Passing duration {Duration} to RenderFinalVideoAsync for segment '\"{SegmentName}\"'.", videoDuration, segmentName);
            await _ffmpegService.RenderFinalVideoAsync(backgroundPath, narrationPath, storyboard, videoDuration, progress, outputSegmentPath, cancellationToken);

            _logger.LogInformation("Successfully generated video segment '\"{SegmentName}\"' at \"{Path}\"", segmentName, outputSegmentPath);
            return outputSegmentPath;
        }
    }
}