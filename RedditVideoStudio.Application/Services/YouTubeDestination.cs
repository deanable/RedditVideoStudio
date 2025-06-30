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
    public class YouTubeDestination : IVideoDestination
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<YouTubeDestination> _logger;
        private UserCredential? _credential;

        public YouTubeDestination(IAppConfiguration appConfiguration, ILogger<YouTubeDestination> logger)
        {
            _appConfiguration = appConfiguration;
            _logger = logger;
        }

        public string Name => "YouTube";
        public bool IsAuthenticated => _credential != null;

        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            var secrets = await _appConfiguration.GetYouTubeSecretsAsync(cancellationToken);
            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                new[] { GoogleYouTubeService.Scope.YoutubeUpload },
                "user",
                cancellationToken,
                new FileDataStore("YouTube.Api.Auth.Store", true));
        }

        public Task SignOutAsync()
        {
            _credential = null;
            return Task.CompletedTask;
        }

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
                    CategoryId = "20",
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