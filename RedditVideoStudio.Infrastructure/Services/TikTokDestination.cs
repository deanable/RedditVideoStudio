namespace RedditVideoStudio.Infrastructure.Services
{
    using Microsoft.Extensions.Logging;
    using RedditVideoStudio.Core.Interfaces;
    using RedditVideoStudio.Domain.Models;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class TikTokDestination : IVideoDestination
    {
        private readonly TikTokAuthService _authService;
        private readonly ITikTokServiceFactory _tikTokServiceFactory;
        private readonly ILogger<TikTokDestination> _logger;
        private string? _accessToken;

        public string Name => "TikTok";
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        public TikTokDestination(TikTokAuthService authService, ITikTokServiceFactory tikTokServiceFactory, ILogger<TikTokDestination> logger)
        {
            _authService = authService;
            _tikTokServiceFactory = tikTokServiceFactory;
            _logger = logger;
        }

        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting TikTok authentication flow...");
            _accessToken = await _authService.AuthorizeAndGetTokenAsync(cancellationToken);
            _logger.LogInformation("TikTok authentication successful. Access token obtained.");
        }

        public Task SignOutAsync()
        {
            _accessToken = null;
            _logger.LogInformation("Signed out from TikTok.");
            return Task.CompletedTask;
        }

        public Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, string? thumbnailPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                throw new InvalidOperationException("Not authenticated with TikTok.");
            }
            // TikTok does not support custom thumbnail uploads via this API version, so we ignore thumbnailPath.
            var uploader = _tikTokServiceFactory.Create(_accessToken);
            return uploader.UploadVideoAsync(videoPath, videoDetails.Title, cancellationToken);
        }
    }
}
