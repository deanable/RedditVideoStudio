// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Application\Services\PublishingService.cs

namespace RedditVideoStudio.Application.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using RedditVideoStudio.Core.Interfaces;
    using RedditVideoStudio.Domain.Models;
    using RedditVideoStudio.Shared.Models;
    using RedditVideoStudio.Shared.Utilities;

    public class PublishingService : IPublishingService
    {
        private readonly IVideoComposer _videoComposer;
        private readonly IEnumerable<IVideoDestination> _allDestinations;
        private readonly ILogger<PublishingService> _logger;
        private readonly ITempDirectoryFactory _tempDirectoryFactory;
        private readonly IPexelsService _pexelsService;
        private readonly IImageService _imageService;
        private readonly IAppConfiguration _appConfig;

        public PublishingService(
            IVideoComposer videoComposer,
            IEnumerable<IVideoDestination> allDestinations,
            ILogger<PublishingService> logger,
            ITempDirectoryFactory tempDirectoryFactory,
            IPexelsService pexelsService,
            IImageService imageService,
            IAppConfiguration appConfig)
        {
            _videoComposer = videoComposer;
            _allDestinations = allDestinations;
            _logger = logger;
            _tempDirectoryFactory = tempDirectoryFactory;
            _pexelsService = pexelsService;
            _imageService = imageService;
            _appConfig = appConfig;
        }

        public async Task PublishVideoAsync(RedditPostData post, IEnumerable<string> destinationNames, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            var targetDestinations = _allDestinations.Where(d => destinationNames.Contains(d.Name)).ToList();
            if (!targetDestinations.Any())
            {
                _logger.LogWarning("Publishing was requested, but no target destinations were found or specified.");
                return;
            }

            using (var tempDirectory = _tempDirectoryFactory.Create())
            {
                string? thumbnailPath = await GenerateThumbnailForPost(post, tempDirectory.Path, cancellationToken);

                foreach (var destination in targetDestinations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _logger.LogInformation("Processing post '{Title}' for destination '{Destination}'.", post.Title, destination.Name);

                    var orientation = GetOrientationForDestination(destination.Name);
                    _logger.LogInformation("Required orientation for {Destination}: {Orientation}", destination.Name, orientation);

                    string baseFilename = FileUtils.SanitizeFileName(post.Title ?? "video").Take(40).Aggregate("", (s, c) => s + c);
                    string finalVideoPath = Path.Combine(tempDirectory.Path, $"{baseFilename}_{destination.Name}_{Guid.NewGuid().ToString().Substring(0, 4)}.mp4");

                    await _videoComposer.ComposeVideoAsync(post.Title ?? "", post.Comments, progress, cancellationToken, finalVideoPath, orientation);

                    if (!destination.IsAuthenticated)
                    {
                        _logger.LogInformation("Authenticating with {Destination}...", destination.Name);
                        await destination.AuthenticateAsync(cancellationToken);
                    }

                    _logger.LogInformation("Uploading video to {Destination}...", destination.Name);

                    // --- SEO FIX 1: Add a standardized header to the description ---
                    var descriptionHeader = $"From r/{post.Subreddit} on Reddit. Original post: https://reddit.com{post.Permalink}\n\n---\n\n";
                    var videoDescription = $"{descriptionHeader}{post.Title}\n\n{string.Join("\n\n", post.Comments.Take(3))}";

                    // --- SEO FIX 2: Prepend the subreddit to the title ---
                    var videoTitle = $"(r/{post.Subreddit}) - {post.Title ?? "Reddit Story"}";

                    // --- SEO FIX 3: Add default tags for the subreddit ---
                    var videoTags = new[] { "reddit", post.Subreddit };

                    await destination.UploadVideoAsync(
                        finalVideoPath,
                        new VideoDetails
                        {
                            Title = videoTitle,
                            Description = videoDescription,
                            Tags = videoTags,
                            ScheduledPublishTime = post.ScheduledPublishTimeUtc
                        },
                        thumbnailPath,
                        cancellationToken);

                    _logger.LogInformation("Successfully uploaded video for post '{Title}' to {Destination}.", post.Title, destination.Name);
                }
            }
        }

        private async Task<string?> GenerateThumbnailForPost(RedditPostData post, string tempDirectory, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Generating thumbnail for post: '{Title}'", post.Title);
                var thumbnailSettings = _appConfig.Settings.ImageGeneration;
                string backgroundPath = Path.Combine(tempDirectory, "thumbnail_bg.jpg");
                string thumbnailPath = Path.Combine(tempDirectory, "thumbnail.jpg");

                await _pexelsService.DownloadRandomImageAsync(thumbnailSettings.ThumbnailPexelsQuery, backgroundPath, cancellationToken);
                await _imageService.GenerateThumbnailAsync(backgroundPath, post.Title ?? "", thumbnailPath, cancellationToken);

                _logger.LogInformation("Thumbnail generated successfully at: {Path}", thumbnailPath);
                return thumbnailPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate thumbnail for post '{Title}'. Upload will continue without a custom thumbnail.", post.Title);
                return null;
            }
        }

        private string GetOrientationForDestination(string destinationName)
        {
            return destinationName.Equals("TikTok", StringComparison.OrdinalIgnoreCase) ||
                   destinationName.Equals("Instagram", StringComparison.OrdinalIgnoreCase)
                ? "Portrait"
                : "Landscape";
        }
    }
}