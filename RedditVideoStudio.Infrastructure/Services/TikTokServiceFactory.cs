using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Interfaces;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the ITikTokServiceFactory to create instances of the TikTokUploader service.
    /// </summary>
    public class TikTokServiceFactory : ITikTokServiceFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public TikTokServiceFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Creates a new instance of the TikTokUploader service with the provided access token.
        /// </summary>
        /// <param name="accessToken">The authenticated user's access token.</param>
        /// <returns>A fully configured ITikTokUploadService instance.</returns>
        public ITikTokUploadService Create(string accessToken)
        {
            return new TikTokUploader(
                _loggerFactory.CreateLogger<TikTokUploader>(),
                accessToken);
        }
    }
}