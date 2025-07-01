using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Shared.Models;
using RedditVideoStudio.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Application.Services
{
    public class VideoComposer : IVideoComposer
    {
        private readonly ILogger<VideoComposer> _logger;
        private readonly IRedditService _redditService;
        private readonly IFfmpegService _ffmpegService;
        private readonly IAppConfiguration _appConfig;
        private readonly IStoryboardGenerator _storyboardGenerator;
        private readonly IVideoSegmentGenerator _videoSegmentGenerator;
        private readonly ITempDirectoryFactory _tempDirectoryFactory;

        public VideoComposer(
            ILogger<VideoComposer> logger,
            IRedditService redditService,
            IFfmpegService ffmpegService,
            IAppConfiguration appConfig,
            IStoryboardGenerator storyboardGenerator,
            IVideoSegmentGenerator videoSegmentGenerator,
            ITempDirectoryFactory tempDirectoryFactory)
        {
            _logger = logger;
            _redditService = redditService;
            _ffmpegService = ffmpegService;
            _appConfig = appConfig;
            _storyboardGenerator = storyboardGenerator;
            _videoSegmentGenerator = videoSegmentGenerator;
            _tempDirectoryFactory = tempDirectoryFactory;
        }

        public async Task ComposeVideoAsync(string title, List<string> comments, IProgress<ProgressReport> progress, CancellationToken cancellationToken, string outputPath, string orientation)
        {
            var originalOrientation = _appConfig.Settings.Ffmpeg.VideoOrientation;
            try
            {
                // Temporarily set the orientation for this specific video composition
                _appConfig.Settings.Ffmpeg.VideoOrientation = orientation;
                _logger.LogInformation("Starting video composition for: '{Title}' with orientation: {Orientation}", title, orientation);

                var clipSettings = _appConfig.Settings.ClipSettings;
                var videoSegments = new List<string>();

                using (var tempDirectory = _tempDirectoryFactory.Create())
                {
                    if (!string.IsNullOrEmpty(clipSettings.IntroPath) && File.Exists(clipSettings.IntroPath) && clipSettings.IntroDuration > 0)
                    {
                        var introPath = await ProcessStaticClip(clipSettings.IntroPath, clipSettings.IntroDuration, "intro", tempDirectory.Path, cancellationToken);
                        videoSegments.Add(introPath);
                    }

                    var titleStoryboard = await _storyboardGenerator.GenerateAsync(new[] { title }, tempDirectory.Path, "Title", progress, cancellationToken);
                    var titleSegmentPath = await _videoSegmentGenerator.GenerateAsync(titleStoryboard, "abstract", tempDirectory.Path, "Title", Path.Combine(tempDirectory.Path, "title_clip.mp4"), progress, cancellationToken);
                    videoSegments.Add(titleSegmentPath);

                    string? breakClipPath = null;
                    if (!string.IsNullOrEmpty(clipSettings.BreakClipPath) && File.Exists(clipSettings.BreakClipPath) && clipSettings.BreakClipDuration > 0)
                    {
                        breakClipPath = await ProcessStaticClip(clipSettings.BreakClipPath, clipSettings.BreakClipDuration, "break", tempDirectory.Path, cancellationToken);
                    }

                    if (comments.Any())
                    {
                        for (int i = 0; i < comments.Count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!string.IsNullOrEmpty(breakClipPath))
                            {
                                videoSegments.Add(breakClipPath);
                            }
                            var comment = comments[i];
                            var commentSegmentName = $"Comment_{i + 1}";
                            var commentStoryboard = await _storyboardGenerator.GenerateAsync(new[] { comment }, tempDirectory.Path, commentSegmentName, progress, cancellationToken);
                            var commentSegmentPath = await _videoSegmentGenerator.GenerateAsync(commentStoryboard, "nature", tempDirectory.Path, commentSegmentName, Path.Combine(tempDirectory.Path, $"{commentSegmentName}_clip.mp4"), progress, cancellationToken);
                            videoSegments.Add(commentSegmentPath);
                        }
                    }

                    if (!string.IsNullOrEmpty(clipSettings.OutroPath) && File.Exists(clipSettings.OutroPath) && clipSettings.OutroDuration > 0)
                    {
                        var outroPath = await ProcessStaticClip(clipSettings.OutroPath, clipSettings.OutroDuration, "outro", tempDirectory.Path, cancellationToken);
                        videoSegments.Add(outroPath);
                    }

                    progress.Report(new ProgressReport { Percentage = 95, Message = "Stitching final video..." });
                    await _ffmpegService.ConcatenateVideosAsync(videoSegments, outputPath, cancellationToken);

                    _logger.LogInformation("Final video rendered successfully at: {Path}", outputPath);
                    progress.Report(new ProgressReport { Percentage = 100, Message = "Video composition complete." });
                }
            }
            finally
            {
                // IMPORTANT: Restore the original setting
                _appConfig.Settings.Ffmpeg.VideoOrientation = originalOrientation;
            }
        }

        private async Task<string> ProcessStaticClip(string clipPath, double duration, string clipName, string tempPath, CancellationToken cancellationToken)
        {
            var tempClipPath = clipPath;

            if (duration > 0)
            {
                var trimmedPath = Path.Combine(tempPath, $"{clipName}_trimmed.mp4");
                tempClipPath = await _ffmpegService.TrimVideoAsync(clipPath, TimeSpan.FromSeconds(duration), trimmedPath, cancellationToken);
            }

            var finalClipPath = Path.Combine(tempPath, $"{clipName}_final.mp4");
            await _ffmpegService.NormalizeVideoAsync(tempClipPath, finalClipPath, cancellationToken);
            return finalClipPath;
        }

        public Task ComposeVideoAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Batch processing for multiple platforms is not yet implemented.");
        }
    }
}