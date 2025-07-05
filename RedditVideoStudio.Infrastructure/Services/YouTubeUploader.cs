// In: C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\YouTubeUploader.cs

using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Shared.Configuration;
using RedditVideoStudio.Shared.Models; // Add this using statement
using RedditVideoStudio.Shared.Utilities;
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
        private readonly YouTubeService _youTubeService;
        private readonly AppSettings _settings;

        public YouTubeUploader(
             ILogger<YouTubeUploader> logger,
             AppSettings settings,
             YouTubeService youtubeService)
        {
            _logger = logger;
            _settings = settings;
            _youTubeService = youtubeService;
        }

        // --- START OF MODIFICATION: Updated method signature ---
        public async Task<string?> UploadVideoAsync(
            string videoPath,
            string thumbnailPath,
            string title,
            string description,
            DateTime? scheduledPublishTimeUtc,
            IProgress<ProgressReport> progress,
            CancellationToken cancellationToken = default)
        // --- END OF MODIFICATION ---
        {
            if (!File.Exists(videoPath)) throw new FileNotFoundException("Video file not found for upload.", videoPath);
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Video title cannot be null or empty.", nameof(title));

            var video = new Video
            {
                Snippet = new VideoSnippet
                {
                    Title = TextUtils.SanitizeYouTubeTitle(title),
                    Description = description ?? "",
                    CategoryId = "22"
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = _settings.YouTube.PrivacyStatus,
                }
            };

            if (_settings.YouTube.PrivacyStatus.Equals("private", StringComparison.OrdinalIgnoreCase) && scheduledPublishTimeUtc.HasValue)
            {
                // --- FIXED: Used non-obsolete property ---
                video.Status.PublishAtDateTimeOffset = scheduledPublishTimeUtc.Value.ToUniversalTime();
            }

            FileStream? fileStream = null;
            try
            {
                fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read);
                _logger.LogInformation("Uploading video with title: '{Title}'", video.Snippet.Title);

                var insertRequest = _youTubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");

                // --- START OF MODIFICATION: Handle progress reporting ---
                var fileSize = fileStream.Length;
                insertRequest.ProgressChanged += (IUploadProgress uploadProgress) =>
                {
                    switch (uploadProgress.Status)
                    {
                        case UploadStatus.Uploading:
                            var percentage = (int)(((double)uploadProgress.BytesSent / fileSize) * 100);
                            progress.Report(new ProgressReport { Percentage = percentage, Message = $"Uploading to YouTube... {percentage}%" });
                            break;
                        case UploadStatus.Failed:
                            _logger.LogError(uploadProgress.Exception, "YouTube upload failed.");
                            break;
                    }
                };
                // --- END OF MODIFICATION ---

                insertRequest.ResponseReceived += OnUploadResponse;

                var uploadStatus = await insertRequest.UploadAsync(cancellationToken);

                if (uploadStatus.Status != UploadStatus.Completed)
                {
                    var errorMessage = uploadStatus.Exception?.Message ?? "Unknown upload error.";
                    throw new YouTubeApiException($"YouTube upload failed: {errorMessage}", uploadStatus.Exception);
                }

                string videoId = insertRequest.ResponseBody.Id;
                _logger.LogInformation("Video uploaded to YouTube with ID: {VideoId}", videoId);

                if (!string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath))
                {
                    _logger.LogInformation("Uploading thumbnail for video ID: {VideoId}", videoId);
                    await using var thumbStream = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read);
                    var thumbRequest = _youTubeService.Thumbnails.Set(videoId, thumbStream, "image/jpeg");

                    var thumbUploadStatus = await thumbRequest.UploadAsync(cancellationToken);
                    if (thumbUploadStatus.Status != UploadStatus.Completed)
                    {
                        var errorMessage = thumbUploadStatus.Exception?.Message ?? "Unknown thumbnail upload error.";
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

        // ... (OnUploadResponse and FetchUploadedVideoTitlesAsync methods remain the same) ...
        private void OnUploadResponse(Video video)
        {
            _logger.LogInformation("Video upload response received. Video ID: {VideoId}", video.Id);
        }

        public async Task<List<string>> FetchUploadedVideoTitlesAsync(CancellationToken cancellationToken)
        {
            var videoTitles = new HashSet<string>();
            var nextPageToken = "";
            _logger.LogInformation("Fetching all uploaded video titles from YouTube...");
            while (nextPageToken != null)
            {
                var searchRequest = _youTubeService.Search.List("snippet");
                searchRequest.ForMine = true;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 50;
                searchRequest.PageToken = nextPageToken;

                var searchResponse = await searchRequest.ExecuteAsync(cancellationToken);

                foreach (var searchResult in searchResponse.Items)
                {
                    var sanitizedTitle = TextUtils.SanitizeYouTubeTitle(searchResult.Snippet.Title);
                    if (!videoTitles.Add(sanitizedTitle))
                    {
                        _logger.LogWarning("Found duplicate video title on YouTube channel: \"{Title}\"", sanitizedTitle);
                    }
                }

                nextPageToken = searchResponse.NextPageToken;
            }
            _logger.LogInformation("Finished fetching video titles. Found {Count} unique titles.", videoTitles.Count);
            return videoTitles.ToList();
        }
    }
}