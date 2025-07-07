// File: C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\TikTokDestination.cs

using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class TikTokDestination : IVideoDestination
    {
        private readonly ILogger<TikTokDestination> _logger;
        private readonly ITikTokAuthService _authService;
        private readonly ITikTokServiceFactory _tikTokServiceFactory;
        private readonly IAppConfiguration _config;
        private string? _accessToken;
        private string? _refreshToken;

        public TikTokDestination(
            ILogger<TikTokDestination> logger,
            ITikTokAuthService authService,
            ITikTokServiceFactory tikTokServiceFactory,
            IAppConfiguration config)
        {
            _logger = logger;
            _authService = authService;
            _tikTokServiceFactory = tikTokServiceFactory;
            _config = config;
        }

        public string Name => "TikTok";

        /// <summary>
        /// Gets a value indicating whether the destination is currently authenticated.
        /// This is determined by the presence of an access token.
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting TikTok authentication process.");

            // CORRECTED: The line 'IsAuthenticated = false;' was removed.
            // The state is correctly reset by nullifying the tokens below.
            _accessToken = null;
            _refreshToken = null;

            // 1. Generate a unique state parameter for CSRF protection.
            var state = Guid.NewGuid().ToString();
            var redirectUri = _config.Settings.TikTok.RedirectUri;

            // 2. Get the authorization URL from the auth service.
            var authUrl = _authService.GetAuthorizationUrl(state);

            try
            {
                // 3. Open the URL in the user's default browser.
                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
                _logger.LogInformation("Opened browser for user authentication.");

                // 4. Listen for the callback from TikTok's servers.
                var authCode = await ListenForCallbackAsync(redirectUri, state, cancellationToken);
                _logger.LogInformation("Received authorization code, exchanging for access token...");

                // 5. Exchange the received authorization code for tokens.
                var tokenResponse = await _authService.ExchangeCodeForTokensAsync(authCode, state, state);

                // 6. Store the tokens. The IsAuthenticated property will now automatically become true.
                _accessToken = tokenResponse.AccessToken;
                _refreshToken = tokenResponse.RefreshToken;

                if (string.IsNullOrEmpty(_accessToken))
                {
                    throw new ApiException("Authentication with TikTok failed, access token was not received.");
                }

                _logger.LogInformation("Successfully authenticated with TikTok.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during TikTok authentication: {ErrorMessage}", ex.Message);
                _accessToken = null;
                _refreshToken = null;
                throw;
            }
        }

        private async Task<string> ListenForCallbackAsync(string redirectUri, string expectedState, CancellationToken cancellationToken)
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);

            try
            {
                listener.Start();
                _logger.LogInformation("HttpListener started. Waiting for authentication callback on {Uri}", redirectUri);

                var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
                var request = context.Request;
                _logger.LogDebug("Listener received request for URL: {RequestUrl}", request.Url?.ToString() ?? "null");

                string responseString = "<html><body><h1>Authentication successful!</h1><p>You can close this browser window now.</p></body></html>";
                var buffer = Encoding.UTF8.GetBytes(responseString);
                var response = context.Response;
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, cancellationToken);
                response.OutputStream.Close();

                var code = request.QueryString.Get("code");
                var incomingState = request.QueryString.Get("state");
                var error = request.QueryString.Get("error");
                var errorDescription = request.QueryString.Get("error_description");

                _logger.LogInformation("Received callback parameters. Code: '{Code}', State: '{State}'", code ?? "null", incomingState ?? "null");

                if (!string.IsNullOrEmpty(error))
                {
                    throw new ApiException($"TikTok returned an error: {error} - {errorDescription}");
                }

                if (string.IsNullOrEmpty(code) || incomingState != expectedState)
                {
                    throw new ApiException("TikTok authorization failed. State mismatch or no code received.");
                }

                return code;
            }
            finally
            {
                listener.Stop();
                _logger.LogInformation("HttpListener has stopped.");
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
            _refreshToken = null;
            _logger.LogInformation("Signed out from TikTok. Access token has been cleared.");
            return Task.CompletedTask;
        }

        public async Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, string? thumbnailPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_accessToken))
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