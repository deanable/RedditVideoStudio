namespace RedditVideoStudio.Infrastructure.Services
{
    using Microsoft.Extensions.Logging;
    using RedditVideoStudio.Core.Exceptions;
    using RedditVideoStudio.Core.Interfaces;
    using RedditVideoStudio.Domain.Models;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class TikTokDestination : IVideoDestination
    {
        private readonly ILogger<TikTokDestination> _logger;
        private readonly TikTokAuthService _authService;
        private readonly ITikTokServiceFactory _tikTokServiceFactory;
        private string? _accessToken;

        public TikTokDestination(
            ILogger<TikTokDestination> logger,
            TikTokAuthService authService,
            ITikTokServiceFactory tikTokServiceFactory)
        {
            _logger = logger;
            _authService = authService;
            _tikTokServiceFactory = tikTokServiceFactory;
        }

        public string Name => "TikTok";

        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting TikTok authentication process.");
            try // ADDED: More specific error logging
            {
                _accessToken = await _authService.AuthorizeAndGetTokenAsync(cancellationToken);
                if (string.IsNullOrEmpty(_accessToken))
                {
                    throw new ApiException("Authentication with TikTok failed, access token was not received.");
                }
                _logger.LogInformation("Successfully authenticated with TikTok.");
            }
            catch (ApiException ex)
            {
                _logger.LogError(ex, "An API error occurred during TikTok authentication: {ErrorMessage}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A critical error occurred during TikTok authentication.");
                throw new ApiException("A critical error occurred during TikTok authentication.", ex);
            }
        }

        public Task<bool> DoesVideoExistAsync(string title, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Checking if a video exists on TikTok is not yet implemented.");
            return Task.FromResult(false);
        }

        public Task<HashSet<string>> GetUploadedVideoTitlesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Fetching uploaded video titles from TikTok is not yet implemented.");
            return Task.FromResult(new HashSet<string>());
        }

        public Task SignOutAsync()
        {
            _accessToken = null;
            _logger.LogInformation("Signed out from TikTok. Access token has been cleared.");
            return Task.CompletedTask;
        }

        public async Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, string? thumbnailPath, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated || string.IsNullOrEmpty(_accessToken))
            {
                throw new InvalidOperationException("Cannot upload to TikTok: Not authenticated.");
            }

            _logger.LogInformation("Creating TikTok upload service instance.");
            var tikTokUploader = _tikTokServiceFactory.Create(_accessToken);

            await tikTokUploader.UploadVideoAsync(videoPath, videoDetails.Title, cancellationToken);
            _logger.LogInformation("Successfully initiated upload process for video '{Title}' to TikTok.", videoDetails.Title);
        }
    }
}