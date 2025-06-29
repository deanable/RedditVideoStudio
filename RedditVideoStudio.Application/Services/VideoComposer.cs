// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Application\Services\VideoComposer.cs

using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
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
    /// <summary>
    /// Implements the IVideoComposer interface, acting as the main orchestrator for the video creation process.
    /// It coordinates various services to fetch content, generate storyboards, render video segments,
    /// and combine them into a final video.
    /// </summary>
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

        public async Task ComposeVideoAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            var posts = await _redditService.FetchFullPostDataAsync(cancellationToken);
            var postCount = posts.Count;
            var postIndex = 0;

            foreach (var post in posts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var overallProgress = new Progress<ProgressReport>(report =>
                {
                    var startPercentage = (int)(((double)postIndex / postCount) * 100);
                    var endPercentage = (int)(((double)(postIndex + 1) / postCount) * 100);
                    var range = endPercentage - startPercentage;
                    var scaledPercentage = startPercentage + (int)((double)report.Percentage / 100 * range);

                    progress.Report(new ProgressReport
                    {
                        Percentage = scaledPercentage,
                        Message = $"Video {postIndex + 1}/{postCount}: {report.Message}"
                    });
                });

                var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
                string baseFilename = FileUtils.SanitizeFileName(post.Title!).Take(40).Aggregate("", (s, c) => s + c);
                string finalVideoPath = Path.Combine(outputDir, $"{baseFilename}_output.mp4");

                await ComposeVideoAsync(post.Title!, post.Comments, overallProgress, cancellationToken, finalVideoPath);
                postIndex++;
            }
        }

        public async Task ComposeVideoAsync(string title, List<string> comments, IProgress<ProgressReport> progress, CancellationToken cancellationToken, string outputPath)
        {
            _logger.LogInformation("Starting video composition for: {Title}", title);
            var clipSettings = _appConfig.Settings.ClipSettings;
            var videoSegments = new List<string>();

            using (var tempDirectory = _tempDirectoryFactory.Create())
            {
                try
                {
                    // --- Intro Clip ---
                    if (!string.IsNullOrEmpty(clipSettings.IntroPath) && File.Exists(clipSettings.IntroPath))
                    {
                        var introPath = await ProcessStaticClip(clipSettings.IntroPath, clipSettings.IntroDuration, "intro", tempDirectory.Path, cancellationToken);
                        videoSegments.Add(introPath);
                    }

                    // --- Title Segment ---
                    var titleStoryboard = await _storyboardGenerator.GenerateAsync(new[] { title }, tempDirectory.Path, "Title", progress, cancellationToken);
                    var titleSegmentPath = await _videoSegmentGenerator.GenerateAsync(titleStoryboard, "abstract", tempDirectory.Path, "Title", Path.Combine(tempDirectory.Path, "title_clip.mp4"), progress, cancellationToken);
                    videoSegments.Add(titleSegmentPath);

                    // --- Process the break clip once before the loop ---
                    string? breakClipPath = null;
                    if (!string.IsNullOrEmpty(clipSettings.BreakClipPath) && File.Exists(clipSettings.BreakClipPath))
                    {
                        breakClipPath = await ProcessStaticClip(clipSettings.BreakClipPath, clipSettings.BreakClipDuration, "break", tempDirectory.Path, cancellationToken);
                    }

                    // --- Process Comments and Separators ---
                    if (comments.Any())
                    {
                        for (int i = 0; i < comments.Count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Add the pre-processed separator clip before each comment
                            if (!string.IsNullOrEmpty(breakClipPath))
                            {
                                videoSegments.Add(breakClipPath);
                                _logger.LogInformation("Added break clip for comment {CommentNumber}", i + 1);
                            }

                            // Generate segment for the current comment
                            var comment = comments[i];
                            var commentSegmentName = $"Comment_{i + 1}";
                            _logger.LogInformation("Generating video segment for {SegmentName}", commentSegmentName);

                            var commentStoryboard = await _storyboardGenerator.GenerateAsync(new[] { comment }, tempDirectory.Path, commentSegmentName, progress, cancellationToken);
                            var commentSegmentPath = await _videoSegmentGenerator.GenerateAsync(commentStoryboard, "nature", tempDirectory.Path, commentSegmentName, Path.Combine(tempDirectory.Path, $"{commentSegmentName}_clip.mp4"), progress, cancellationToken);
                            videoSegments.Add(commentSegmentPath);
                        }
                    }

                    // --- Outro Clip ---
                    if (!string.IsNullOrEmpty(clipSettings.OutroPath) && File.Exists(clipSettings.OutroPath))
                    {
                        var outroPath = await ProcessStaticClip(clipSettings.OutroPath, clipSettings.OutroDuration, "outro", tempDirectory.Path, cancellationToken);
                        videoSegments.Add(outroPath);
                    }

                    progress.Report(new ProgressReport { Percentage = 95, Message = "Stitching final video..." });
                    await _ffmpegService.ConcatenateVideosAsync(videoSegments, outputPath, cancellationToken);

                    _logger.LogInformation("Final video rendered successfully at: {Path}", outputPath);
                    progress.Report(new ProgressReport { Percentage = 100, Message = "Video composition complete." });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the video composition pipeline for title: {Title}", title);
                    throw;
                }
            }
        }

        /// <summary>
        /// Helper method to process static clips (intro, outro, break).
        /// It ensures the clip is trimmed and normalized to project standards.
        /// This is crucial for preventing concatenation errors in FFmpeg.
        /// </summary>
        private async Task<string> ProcessStaticClip(string clipPath, double duration, string clipName, string tempPath, CancellationToken cancellationToken)
        {
            var tempClipPath = clipPath;

            // 1. Trim the clip if a duration is specified. This creates a temporary trimmed clip.
            if (duration > 0)
            {
                var trimmedPath = Path.Combine(tempPath, $"{clipName}_trimmed.mp4");
                tempClipPath = await _ffmpegService.TrimVideoAsync(clipPath, TimeSpan.FromSeconds(duration), trimmedPath, cancellationToken);
            }

            // 2. Normalize the (potentially trimmed) clip to ensure it has the correct encoding, resolution,
            // pixel format, and an audio track for concatenation.
            var finalClipPath = Path.Combine(tempPath, $"{clipName}_final.mp4");
            await _ffmpegService.NormalizeVideoAsync(tempClipPath, finalClipPath, cancellationToken);

            return finalClipPath;
        }
    }
}