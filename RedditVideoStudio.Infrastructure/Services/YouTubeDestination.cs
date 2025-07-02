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
    using System.Text;
    using Google.Apis.Auth.OAuth2.Responses;

    /// <summary>
    /// Represents the YouTube video destination, handling authentication and video uploads.
    /// </summary>
    public class YouTubeDestination : IVideoDestination
    {
        private readonly IAppConfiguration _appConfig;
        private readonly ILogger<YouTubeDestination> _logger;
        private UserCredential? _credential;
        private readonly FileDataStore _fileDataStore;
        private const string CredentialDataStoreKey = "YouTube.Api.Auth.Store";

        /// <summary>
        /// Initializes a new instance of the <see cref="YouTubeDestination"/> class.
        /// </summary>
        /// <param name="appConfiguration">The application configuration service.</param>
        /// <param name="logger">The logger instance.</param>
        public YouTubeDestination(IAppConfiguration appConfiguration, ILogger<YouTubeDestination> logger)
        {
            _appConfig = appConfiguration;
            _logger = logger;
            // The FileDataStore caches the OAuth 2.0 tokens to avoid re-authenticating every time.
            _fileDataStore = new FileDataStore(Path.Combine(AppContext.BaseDirectory, CredentialDataStoreKey), true);
        }

        /// <summary>
        /// Gets the name of the destination.
        /// </summary>
        public string Name => "YouTube";

        /// <summary>
        /// Gets a value indicating whether the user is currently authenticated.
        /// It checks if the credential exists and if the access token has not expired.
        /// </summary>
        public bool IsAuthenticated => _credential != null && !_credential.Token.IsExpired(_credential.Flow.Clock);

        /// <summary>
        /// Initiates the OAuth 2.0 authentication flow to obtain user credentials.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            var youtubeSettings = _appConfig.Settings.YouTube;
            if (string.IsNullOrEmpty(youtubeSettings.ClientId) || string.IsNullOrEmpty(youtubeSettings.ClientSecret))
            {
                _logger.LogError("YouTube Client ID or Client Secret is not configured in settings.");
                throw new AppConfigurationException("YouTube Client ID or Client Secret is not configured in settings. Please configure them in the Settings window.");
            }

            // Construct the client_secrets.json content dynamically from settings.
            // This is crucial for the GoogleWebAuthorizationBroker to work correctly.
            // The credentials MUST be for a "Desktop app" in your Google Cloud project.
            var json = $"{{\"installed\":{{\"client_id\":\"{youtubeSettings.ClientId}\",\"client_secret\":\"{youtubeSettings.ClientSecret}\"}}}}";

            try
            {
                await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
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
                    // This case is unlikely if no exception was thrown, but it's good practice.
                    _logger.LogError("Failed to authenticate with YouTube. The user may have denied access.");
                    throw new YouTubeApiException("Authentication was not successful. The user may have cancelled the operation.");
                }
            }
            catch (TokenResponseException ex)
            {
                _logger.LogError(ex, "A token response error occurred during YouTube authentication. This usually means your Client ID/Secret is incorrect or your Google Cloud project is misconfigured. Error: {ErrorDetails}", ex.Error.ErrorDescription);
                throw new YouTubeApiException($"Authentication failed. Please verify your Google Cloud credentials and project setup. Details: {ex.Error.ErrorDescription}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected exception occurred during YouTube authentication.");
                throw new YouTubeApiException("An unexpected error occurred during authentication.", ex);
            }
        }

        /// <summary>
        /// Signs the user out by clearing the stored credentials.
        /// </summary>
        public async Task SignOutAsync()
        {
            _credential = null;
            // CORRECTED: The DeleteAsync<T> method only takes one argument, the key.
            await _fileDataStore.DeleteAsync<string>("user");
            _logger.LogInformation("User has been signed out from YouTube, and stored credentials have been deleted.");
        }

        /// <summary>
        /// Uploads a video to YouTube, including its metadata and an optional thumbnail.
        /// </summary>
        /// <param name="videoPath">The local path of the video file to upload.</param>
        /// <param name="videoDetails">The details (title, description, etc.) of the video.</param>
        /// <param name="thumbnailPath">The local path of the thumbnail image (optional).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
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
                    CategoryId = "22", // People & Blogs - a default category
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = "private" // Default to private, can be changed in settings
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