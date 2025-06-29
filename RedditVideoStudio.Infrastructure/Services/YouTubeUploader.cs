// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\YouTubeUploader.cs

using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class YouTubeUploader : IYouTubeUploadService
    {
        private readonly ILogger<YouTubeUploader> _logger;
        private readonly YouTubeService _youtubeService;
        private readonly AppSettings _settings;

        public YouTubeUploader(
             ILogger<YouTubeUploader> logger,
             AppSettings settings,
             YouTubeService youtubeService)
        {
            _logger = logger;
            _settings = settings;
            _youtubeService = youtubeService;
        }

        public async Task<string?> UploadVideoAsync(
            string videoPath,
            string thumbnailPath,
            string title,
            string description,
            DateTime? scheduledPublishTimeUtc,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(videoPath)) throw new FileNotFoundException("Video file not found for upload.", videoPath);
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Video title cannot be null or empty.", nameof(title));

            var video = new Video
            {
                Snippet = new VideoSnippet
                {
                    Title = Shared.Utilities.TextUtils.SanitizeYouTubeTitle(title),
                    Description = description ?? "",
                    CategoryId = "22" // A default category, e.g., "People & Blogs"
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = _settings.YouTube.PrivacyStatus,
                }
            };

            if (_settings.YouTube.PrivacyStatus.Equals("private", StringComparison.OrdinalIgnoreCase) && scheduledPublishTimeUtc.HasValue)
            {
                video.Status.PublishAt = scheduledPublishTimeUtc.Value.ToUniversalTime();
            }

            FileStream? fileStream = null;
            try
            {
                fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read);
                _logger.LogInformation("Uploading video with title: '{Title}'", video.Snippet.Title);

                var insertRequest = _youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");
                insertRequest.ProgressChanged += OnUploadProgress;
                insertRequest.ResponseReceived += OnUploadResponse;

                var uploadStatus = await insertRequest.UploadAsync(cancellationToken);

                if (uploadStatus.Status != UploadStatus.Completed)
                {
                    var errorMessage = uploadStatus.Exception?.Message ?? "Unknown upload error.";
                    throw new YouTubeApiException($"YouTube upload failed: {errorMessage}", uploadStatus.Exception);
                }

                string videoId = insertRequest.ResponseBody.Id;
                _logger.LogInformation("Video uploaded to YouTube with ID: {VideoId}", videoId);

                // Check for thumbnail path and existence
                if (!string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath))
                {
                    _logger.LogInformation("Uploading thumbnail for video ID: {VideoId}", videoId);
                    await using var thumbStream = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read);
                    var thumbRequest = _youtubeService.Thumbnails.Set(videoId, thumbStream, "image/jpeg");

                    // CORRECTED: Capture and check the status of the thumbnail upload.
                    var thumbUploadStatus = await thumbRequest.UploadAsync(cancellationToken);

                    if (thumbUploadStatus.Status != UploadStatus.Completed)
                    {
                        var errorMessage = thumbUploadStatus.Exception?.Message ?? "Unknown thumbnail upload error.";
                        // CORRECTED: Throw a specific, more helpful exception.
                        throw new YouTubeApiException($"YouTube thumbnail upload failed: {errorMessage}. Note: This can happen if the video is 'private' and the YouTube channel is not verified for advanced features.", thumbUploadStatus.Exception);
                    }

                    _logger.LogInformation("Thumbnail uploaded successfully for video ID: {VideoId}", videoId);
                }

                return videoId;
            }
            catch (Exception ex) when (ex is not YouTubeApiException)
            {
                _logger.LogError(ex, "An error occurred during YouTube upload for video: {VideoPath}", videoPath);
                throw new YouTubeApiException("An error occurred during the YouTube upload process.", ex);
            }
            finally
            {
                fileStream?.Dispose();
            }
        }

        public async Task<HashSet<string>> FetchUploadedVideoTitlesAsync(CancellationToken cancellationToken)
        {
            var videoTitles = new HashSet<string>();
            _logger.LogInformation("Reading YouTube Channel Videos...");

            var channelsRequest = _youtubeService.Channels.List("contentDetails");
            channelsRequest.Mine = true;

            var channelsResponse = await channelsRequest.ExecuteAsync(cancellationToken);
            var channel = channelsResponse.Items.FirstOrDefault();
            if (channel == null)
            {
                _logger.LogWarning("Could not find user's channel.");
                return videoTitles;
            }

            var uploadsPlaylistId = channel.ContentDetails.RelatedPlaylists.Uploads;
            string? nextPageToken = null;

            do
            {
                var playlistItemsRequest = _youtubeService.PlaylistItems.List("snippet");
                playlistItemsRequest.PlaylistId = uploadsPlaylistId;
                playlistItemsRequest.MaxResults = 50;
                playlistItemsRequest.PageToken = nextPageToken;

                var playlistItemsResponse = await playlistItemsRequest.ExecuteAsync(cancellationToken);

                foreach (var item in playlistItemsResponse.Items)
                {
                    if (item.Snippet.Title != null)
                    {
                        videoTitles.Add(item.Snippet.Title);
                    }
                }

                nextPageToken = playlistItemsResponse.NextPageToken;
            } while (nextPageToken != null && !cancellationToken.IsCancellationRequested);

            _logger.LogInformation("YouTube Channel Videos Read. Found {Count} unique titles.", videoTitles.Count);
            return videoTitles;
        }

        private void OnUploadProgress(IUploadProgress progress)
        {
            if (progress.Status == UploadStatus.Uploading)
            {
                _logger.LogInformation("YouTube upload progress: {BytesSent} bytes sent.", progress.BytesSent);
            }
        }

        private void OnUploadResponse(Video video)
        {
            _logger.LogInformation("Video upload response received. Video ID: {VideoId}", video.Id);
        }
    }
}