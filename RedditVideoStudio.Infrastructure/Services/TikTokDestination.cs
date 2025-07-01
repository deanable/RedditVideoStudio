using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class TikTokDestination : IVideoDestination
    {
        private readonly TikTokAuthService _authService;
        private readonly ITikTokServiceFactory _tikTokServiceFactory;
        private string? _accessToken;

        public string Name => "TikTok";
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        public TikTokDestination(TikTokAuthService authService, ITikTokServiceFactory tikTokServiceFactory)
        {
            _authService = authService;
            _tikTokServiceFactory = tikTokServiceFactory;
        }

        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            // Pass the cancellationToken to the authorization method
            _accessToken = await _authService.AuthorizeAndGetTokenAsync(cancellationToken);
        }

        public Task SignOutAsync()
        {
            _accessToken = null;
            return Task.CompletedTask;
        }

        public Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                throw new InvalidOperationException("Not authenticated with TikTok.");
            }
            var uploader = _tikTokServiceFactory.Create(_accessToken);
            return uploader.UploadVideoAsync(videoPath, videoDetails.Title, cancellationToken);
        }
    }
}