// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\YouTubeDestination.cs

namespace RedditVideoStudio.Infrastructure.Services
{
    using Google.Apis.Auth.OAuth2;
    using Google.Apis.Services;
    using Google.Apis.Upload;
    using Google.Apis.YouTube.v3;
    using Google.Apis.YouTube.v3.Data;
    using Microsoft.Extensions.Logging;
    using Google.Apis.Util.Store;
    using RedditVideoStudio.Core.Interfaces;
    using RedditVideoStudio.Domain.Models;
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using RedditVideoStudio.Core.Exceptions;
    using System.Linq;
    using System.Collections.Generic;
    using RedditVideoStudio.Shared.Utilities;

    /// <summary>
    /// Represents the YouTube video destination, handling authentication, uploading,
    /// and checking for existing videos on the user's channel.
    /// </summary>
    public class YouTubeDestination : IVideoDestination
    {
        private readonly ILogger<YouTubeDestination> _logger;
        private UserCredential? _credential;
        private readonly FileDataStore _fileDataStore;
        private const string CredentialDataStoreKey = "YouTube.Api.Auth.Store";
        private const string ClientSecretFileName = "client_secrets.json";

        /// <summary>
        /// Initializes a new instance of the <see cref="YouTubeDestination"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging information and errors.</param>
        public YouTubeDestination(ILogger<YouTubeDestination> logger)
        {
            _logger = logger;

            // Define the path for storing OAuth 2.0 credentials.
            var dataStorePath = Path.Combine(AppContext.BaseDirectory, CredentialDataStoreKey);

            // Ensure the directory for the FileDataStore exists before creating an instance of it.
            if (!Directory.Exists(dataStorePath))
            {
                _logger.LogInformation("Creating FileDataStore directory at: {Path}", dataStorePath);
                Directory.CreateDirectory(dataStorePath);
            }

            // Initialize the FileDataStore, which will manage the storage and retrieval of user credentials.
            _fileDataStore = new FileDataStore(dataStorePath, true);
        }

        /// <summary>
        /// Gets the name of the destination.
        /// </summary>
        public string Name => "YouTube";

        /// <summary>
        /// Gets a value indicating whether the user is currently authenticated and the token is not expired.
        /// </summary>
        public bool IsAuthenticated => _credential != null && !_credential.Token.IsExpired(_credential.Flow.Clock);

        /// <summary>
        /// Initiates the OAuth 2.0 authentication process with Google for YouTube.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            var clientSecretFilePath = Path.Combine(AppContext.BaseDirectory, ClientSecretFileName);
            if (!File.Exists(clientSecretFilePath))
            {
                var errorMessage = $"Authentication failed: The required '{ClientSecretFileName}' was not found. Please download your 'Desktop app' credentials from the Google Cloud Console, rename the file to '{ClientSecretFileName}', and place it in the application's root directory: {AppContext.BaseDirectory}";
                _logger.LogError(errorMessage);
                throw new AppConfigurationException(errorMessage);
            }

            try
            {
                await using var stream = new FileStream(clientSecretFilePath, FileMode.Open, FileAccess.Read);
                var clientSecrets = await GoogleClientSecrets.FromStreamAsync(stream, cancellationToken);

                // --- FIX: Added 'YoutubeReadonly' scope ---
                // To list and search for videos (to check for duplicates), we need read-only access
                // in addition to the upload permission.
                var scopes = new[] {
                    YouTubeService.Scope.YoutubeUpload,
                    YouTubeService.Scope.YoutubeReadonly
                };

                _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets.Secrets,
                    scopes, // Use the updated scopes array
                    "user",
                    cancellationToken,
                    _fileDataStore);

                if (IsAuthenticated)
                {
                    _logger.LogInformation("Successfully authenticated with YouTube.");
                }
                else
                {
                    throw new YouTubeApiException("Authentication was not successful. The user may have cancelled the operation or there was an issue with the credentials.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected exception occurred during YouTube authentication using '{file}'.", ClientSecretFileName);
                throw new YouTubeApiException($"An unexpected error occurred during authentication. Please ensure '{ClientSecretFileName}' is valid and that you have added 'http://localhost' as a redirect URI in your Google Cloud project.", ex);
            }
        }

        /// <summary>
        /// Signs the user out by clearing their stored credentials.
        /// </summary>
        public async Task SignOutAsync()
        {
            _credential = null;
            try
            {
                // --- FIX: Use FileDataStore.ClearAsync() instead of Directory.Delete() ---
                // This is the correct way to clear the credentials. It removes the token file(s)
                // from within the data store directory but leaves the directory itself intact,
                // preventing the DirectoryNotFoundException on subsequent logins.
                await _fileDataStore.ClearAsync();
                _logger.LogInformation("Successfully cleared YouTube credential data store.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete the YouTube credential data store.");
            }
        }

        /// <summary>
        /// Uploads a video file and its metadata to the authenticated user's YouTube channel.
        /// </summary>
        /// <param name="videoPath">The path to the video file.</param>
        /// <param name="videoDetails">Metadata for the video (title, description, etc.).</param>
        /// <param name="thumbnailPath">The path to the thumbnail image.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, string? thumbnailPath, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated || _credential == null)
            {
                throw new InvalidOperationException("Cannot upload video: Not authenticated with YouTube.");
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = "RedditVideoStudio"
            });

            var video = new Video
            {
                Snippet = new VideoSnippet
                {
                    Title = videoDetails.Title,
                    Description = videoDetails.Description,
                    Tags = videoDetails.Tags,
                    CategoryId = "22", // Category for "People & Blogs"
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = "private" // Default to private
                }
            };

            string videoId;
            await using (var fileStream = new FileStream(videoPath, FileMode.Open))
            {
                var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");
                videosInsertRequest.ResponseReceived += (v) => videoId = v.Id;

                _logger.LogInformation("Starting YouTube upload for '{Title}'", videoDetails.Title);
                var uploadStatus = await videosInsertRequest.UploadAsync(cancellationToken);

                if (uploadStatus.Status != UploadStatus.Completed)
                {
                    throw new YouTubeApiException("YouTube video upload failed.", uploadStatus.Exception);
                }
                videoId = videosInsertRequest.ResponseBody.Id;
                _logger.LogInformation("YouTube upload complete. Video ID: {VideoId}", videoId);
            }

            if (!string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath))
            {
                _logger.LogInformation("Uploading thumbnail for video ID: {VideoId}", videoId);
                await using var thumbStream = new FileStream(thumbnailPath, FileMode.Open);
                var thumbRequest = youtubeService.Thumbnails.Set(videoId, thumbStream, "image/jpeg");
                var thumbUploadStatus = await thumbRequest.UploadAsync(cancellationToken);

                if (thumbUploadStatus.Status != UploadStatus.Completed)
                {
                    _logger.LogError(thumbUploadStatus.Exception, "YouTube thumbnail upload failed.");
                }
                else
                {
                    _logger.LogInformation("Thumbnail uploaded successfully for video ID: {VideoId}", videoId);
                }
            }
        }

        /// <summary>
        /// Checks if a video with the specified title already exists on the channel.
        /// </summary>
        /// <param name="title">The title of the video to check.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True if a video with the exact title exists; otherwise, false.</returns>
        public async Task<bool> DoesVideoExistAsync(string title, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated || _credential == null)
            {
                _logger.LogWarning("Cannot check for existing video: Not authenticated with YouTube.");
                return false;
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = "RedditVideoStudio"
            });

            var searchRequest = youtubeService.Search.List("snippet");
            searchRequest.ForMine = true;
            searchRequest.Type = "video";
            searchRequest.Q = $"\"{title}\""; // Exact title search
            searchRequest.MaxResults = 1;

            try
            {
                var searchResponse = await searchRequest.ExecuteAsync(cancellationToken);
                bool exists = searchResponse.Items.Any(item => item.Snippet.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
                if (exists)
                {
                    _logger.LogWarning("A video with the exact title '{Title}' was found on the YouTube channel.", title);
                }
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search for existing videos on YouTube.");
                return false; // Fail safely
            }
        }

        /// <summary>
        /// Fetches all video titles from the authenticated user's YouTube channel.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A HashSet containing all unique, sanitized video titles.</returns>
        public async Task<HashSet<string>> GetUploadedVideoTitlesAsync(CancellationToken cancellationToken = default)
        {
            var videoTitles = new HashSet<string>();
            if (!IsAuthenticated || _credential == null)
            {
                _logger.LogWarning("Cannot get uploaded titles: Not authenticated with YouTube.");
                return videoTitles;
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = "RedditVideoStudio"
            });

            string? nextPageToken = "";
            _logger.LogInformation("Fetching all uploaded video titles from YouTube...");
            while (nextPageToken != null)
            {
                var searchRequest = youtubeService.Search.List("snippet");
                searchRequest.ForMine = true;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 50; // Max allowed value
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
            return videoTitles;
        }
    }
}