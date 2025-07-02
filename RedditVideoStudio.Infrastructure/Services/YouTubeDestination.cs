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

    public class YouTubeDestination : IVideoDestination
    {
        private readonly ILogger<YouTubeDestination> _logger;
        private UserCredential? _credential;
        private readonly FileDataStore _fileDataStore;
        private const string CredentialDataStoreKey = "YouTube.Api.Auth.Store";
        private const string ClientSecretFileName = "client_secrets.json";

        public YouTubeDestination(ILogger<YouTubeDestination> logger)
        {
            _logger = logger;
            _fileDataStore = new FileDataStore(Path.Combine(AppContext.BaseDirectory, CredentialDataStoreKey), true);
        }

        public string Name => "YouTube";
        public bool IsAuthenticated => _credential != null && !_credential.Token.IsExpired(_credential.Flow.Clock);

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

        public async Task SignOutAsync()
        {
            _credential = null;
            await _fileDataStore.DeleteAsync<string>("user");
            _logger.LogInformation("User has been signed out from YouTube, and stored credentials have been deleted.");
        }

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