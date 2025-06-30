// In: C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Application\Services\YouTubeDestination.cs

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3.Data;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using Microsoft.Extensions.Logging;
using Google.Apis.Util.Store;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
using GoogleYouTubeService = Google.Apis.YouTube.v3.YouTubeService;

namespace RedditVideoStudio.Application.Services
{
    /// <summary>
    /// Implementation of IVideoDestination for uploading videos to YouTube.
    /// </summary>
    public class YouTubeDestination : IVideoDestination
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<YouTubeDestination> _logger;
        private UserCredential? _credential;
        private readonly FileDataStore _fileDataStore;
        private const string CredentialDataStoreKey = "YouTube.Api.Auth.Store";

        /// <summary>
        /// Constructor for the YouTubeDestination service.
        /// </summary>
        /// <param name="appConfiguration">The application configuration service.</param>
        /// <param name="logger">The logging service.</param>
        public YouTubeDestination(IAppConfiguration appConfiguration, ILogger<YouTubeDestination> logger)
        {
            _appConfiguration = appConfiguration;
            _logger = logger;
            // The FileDataStore is responsible for storing the OAuth tokens.
            _fileDataStore = new FileDataStore(CredentialDataStoreKey, true);
        }

        /// <summary>
        /// The display name for this destination.
        /// </summary>
        public string Name => "YouTube";

        /// <summary>
        /// Checks if the user is currently authenticated.
        /// </summary>
        public bool IsAuthenticated => _credential != null;

        /// <summary>
        /// Initiates the OAuth 2.0 authentication flow. This will open a browser for the user to sign in.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            var secrets = await _appConfiguration.GetYouTubeSecretsAsync(cancellationToken);
            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                new[] { GoogleYouTubeService.Scope.YoutubeUpload },
                "user",
                cancellationToken,
                _fileDataStore); // Use the class-level FileDataStore
        }

        /// <summary>
        /// Signs the user out by clearing the in-memory credential and deleting the stored token file.
        /// </summary>
        public async Task SignOutAsync()
        {
            _credential = null;
            // The DeleteAsync method for the FileDataStore only takes the user key as an argument.
            await _fileDataStore.DeleteAsync("user");
            _logger.LogInformation("User has been signed out from YouTube, and stored credentials have been deleted.");
        }

        /// <summary>
        /// Uploads a video to YouTube using the authenticated user's account.
        /// </summary>
        /// <param name="videoPath">The local path to the video file.</param>
        /// <param name="videoDetails">Metadata for the video (title, description, etc.).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated || _credential == null)
            {
                throw new InvalidOperationException("Cannot upload video: Not authenticated with YouTube.");
            }

            var youtubeService = new GoogleYouTubeService(new BaseClientService.Initializer()
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
                    CategoryId = "20", // "Gaming" category, can be configured
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = videoDetails.IsPrivate ? "private" : "public"
                }
            };

            using var fileStream = new FileStream(videoPath, FileMode.Open);
            var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");
            await videosInsertRequest.UploadAsync(cancellationToken);
        }
    }
}