using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Interfaces;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the IYouTubeServiceFactory to create instances of the YouTubeUploader service.
    /// This factory pattern is used to decouple the UI's interactive authentication flow
    /// from the core application logic.
    /// </summary>
    public class YouTubeServiceFactory : IYouTubeServiceFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IAppConfiguration _appConfig;

        public YouTubeServiceFactory(ILoggerFactory loggerFactory, IAppConfiguration appConfig)
        {
            _loggerFactory = loggerFactory;
            _appConfig = appConfig;
        }

        /// <summary>
        /// Creates a new instance of the YouTubeUploader service.
        /// </summary>
        /// <param name="credential">The authenticated user credential obtained from the UI layer.</param>
        /// <returns>A fully configured IYouTubeUploadService instance.</returns>
        public IYouTubeUploadService Create(UserCredential credential)
        {
            var youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "RedditVideoStudio"
            });

            return new YouTubeUploader(
                _loggerFactory.CreateLogger<YouTubeUploader>(),
                _appConfig.Settings,
                youtubeService);
        }
    }
}
