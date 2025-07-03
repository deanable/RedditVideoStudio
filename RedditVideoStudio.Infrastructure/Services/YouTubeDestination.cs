// In: C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\YouTubeDestination.cs

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

    /// <summary>
    /// Represents the YouTube video destination, handling authentication and video uploads.
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
        /// <param name="logger">The logger instance for logging.</param>
        public YouTubeDestination(ILogger<YouTubeDestination> logger)
        {
            _logger = logger;

            var dataStorePath = Path.Combine(AppContext.BaseDirectory, CredentialDataStoreKey);

            if (!Directory.Exists(dataStorePath))
            {
                _logger.LogInformation("Creating FileDataStore directory at: {Path}", dataStorePath);
                Directory.CreateDirectory(dataStorePath);
            }

            _fileDataStore = new FileDataStore(dataStorePath, true);
        }

        /// <summary>
        /// Gets the name of the video destination.
        /// </summary>
        public string Name => "YouTube";

        /// <summary>
        /// Gets a value indicating whether the user is currently authenticated.
        /// </summary>
        public bool IsAuthenticated => _credential != null && !_credential.Token.IsExpired(_credential.Flow.Clock);

        /// <summary>
        /// Authenticates the user with YouTube using OAuth 2.0.
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

                _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets.Secrets,
                    new[] { YouTubeService.Scope.YoutubeUpload },
                    "user",
                    cancellationToken,
                    _fileDataStore);

                if (IsAuthenticated)
                {
                    _logger.LogInformation("Successfully authenticated with YouTube.");
                }
                else
                {
                    throw new YouTubeApiException("Authentication was not successful. The user may have cancelled the operation.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected exception occurred during YouTube authentication using '{file}'.", ClientSecretFileName);
                throw new YouTubeApiException($"An unexpected error occurred during authentication. Please ensure '{ClientSecretFileName}' is valid and that you have added 'http://localhost' as a redirect URI in your Google Cloud project.", ex);
            }
        }

        /// <summary>
        /// Signs the user out by revoking the stored credentials and deleting the token directory.
        /// </summary>
        public Task SignOutAsync()
        {
            // --- START OF CORRECTION ---
            _credential = null;
            try
            {
                // Define the path to the directory where authentication tokens are stored.
                var dataStorePath = Path.Combine(AppContext.BaseDirectory, CredentialDataStoreKey);

                // Check if the directory exists before attempting to delete it.
                if (Directory.Exists(dataStorePath))
                {
                    // Recursively delete the directory and all its contents.
                    Directory.Delete(dataStorePath, true);
                    _logger.LogInformation("Successfully deleted YouTube credential data store at: {Path}", dataStorePath);
                }
                else
                {
                    _logger.LogWarning("YouTube credential data store not found at {Path}, nothing to delete.", dataStorePath);
                }
            }
            catch (Exception ex)
            {
                // Log any errors that occur during the deletion process.
                _logger.LogError(ex, "Failed to delete the YouTube credential data store.");
            }
            // Return a completed task as this operation is synchronous.
            return Task.CompletedTask;
            // --- END OF CORRECTION ---
        }

        /// <summary>
        /// Uploads a video to YouTube, including its details and an optional thumbnail.
        /// </summary>
        /// <param name="videoPath">The local path of the video file to upload.</param>
        /// <param name="videoDetails">The metadata for the video (title, description, etc.).</param>
        /// <param name="thumbnailPath">The local path of the thumbnail image (optional).</param>
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
                    CategoryId = "22",
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = "private"
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
    }
}