// System using statements
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Web;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Net;

// Third-party using statements
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Flurl.Http;
using SKIT.FlurlHttpClient.ByteDance.TikTokGlobal;
using SKIT.FlurlHttpClient.ByteDance.TikTokGlobal.Models;

// Application-specific using statements
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Core.Exceptions;

// IMPORTANT: Add this using statement for the Windows Forms dialog
using System.Windows.Forms;
using System.Drawing;


namespace RedditVideoStudio.Infrastructure.Services
{
    public class TikTokDestination : IVideoDestination
    {
        private readonly ILogger<TikTokDestination> _logger;
        private readonly IAppConfiguration _appConfig;
        private readonly string _tokenStorePath;
        private TikTokTokenData? _tokenData;

        private class TikTokApiTokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonProperty("refresh_token")]
            public string RefreshToken { get; set; } = string.Empty;

            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }
        }

        private class TikTokTokenData
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public DateTime ExpiresAtUtc { get; set; }
        }

        public TikTokDestination(ILogger<TikTokDestination> logger, IAppConfiguration appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
            _tokenStorePath = Path.Combine(AppContext.BaseDirectory, "TikTok.Api.Auth.Store", "tiktok-token.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_tokenStorePath)!);
            LoadToken();
        }

        public string Name => "TikTok";

        public bool IsAuthenticated => _tokenData != null && _tokenData.ExpiresAtUtc > DateTime.UtcNow;

        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            var tiktokSettings = _appConfig.Settings.TikTok;
            if (string.IsNullOrEmpty(tiktokSettings.ClientKey) || string.IsNullOrEmpty(tiktokSettings.ClientSecret))
            {
                throw new AppConfigurationException("TikTok Client Key or Client Secret is not configured in settings.");
            }

            string state = Guid.NewGuid().ToString();
            string codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(codeVerifier);
            string redirectUrl = "http://localhost:8912/callback/";

            var authUrl = "https://www.tiktok.com/v2/auth/authorize/" +
                          $"?client_key={tiktokSettings.ClientKey}" +
                          $"&scope={tiktokSettings.Scopes}" +
                          "&response_type=code" +
                          $"&redirect_uri={HttpUtility.UrlEncode(redirectUrl)}" +
                          $"&state={state}" +
                          $"&code_challenge={codeChallenge}" +
                          "&code_challenge_method=S256";

            Task<HttpListenerContext> listenerContextTask = ListenForCallbackAsync(redirectUrl, cancellationToken);
            _logger.LogInformation("HTTP listener started at {Url}. Opening browser for user authentication...", redirectUrl);

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            HttpListenerContext context = await listenerContextTask;
            _logger.LogInformation("Callback received from browser.");

            var queryParams = context.Request.QueryString;
            string? authCode = queryParams.Get("code");
            string? incomingState = queryParams.Get("state");

            if (string.IsNullOrEmpty(authCode) || incomingState != state)
            {
                throw new ApiException("TikTok authorization failed. State mismatch or missing code.");
            }

            _logger.LogInformation("Received authorization code, exchanging for access token...");

            var tokenRequestBody = new Dictionary<string, string>
            {
                { "client_key", tiktokSettings.ClientKey },
                { "client_secret", tiktokSettings.ClientSecret },
                { "code", authCode },
                { "grant_type", "authorization_code" },
                { "redirect_uri", redirectUrl },
                { "code_verifier", codeVerifier }
            };

            try
            {
                var response = await "https://open.tiktokapis.com/v2/oauth/token/"
                    .PostUrlEncodedAsync(tokenRequestBody, cancellationToken: cancellationToken)
                    .ReceiveJson<TikTokApiTokenResponse>();

                if (string.IsNullOrEmpty(response?.AccessToken))
                {
                    throw new ApiException($"Failed to get TikTok access token. The response did not contain a token.");
                }

                _tokenData = new TikTokTokenData
                {
                    AccessToken = response.AccessToken,
                    RefreshToken = response.RefreshToken,
                    ExpiresAtUtc = DateTime.UtcNow.AddSeconds(response.ExpiresIn - 300)
                };
                SaveToken();
                _logger.LogInformation("Successfully authenticated with TikTok and saved token.");
            }
            catch (FlurlHttpException ex)
            {
                var errorBody = await ex.GetResponseStringAsync();
                _logger.LogError(ex, "TikTok token exchange failed with status {StatusCode}. Response Body: {ErrorBody}", ex.StatusCode, errorBody);
                throw new ApiException($"Failed to get TikTok access token. Server responded with an error: {errorBody}", ex);
            }
        }

        public Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, string? thumbnailPath, CancellationToken cancellationToken)
        {
            if (!IsAuthenticated || _tokenData == null)
            {
                throw new InvalidOperationException("Not authenticated with TikTok. Please authenticate first.");
            }

            var tiktokSettings = _appConfig.Settings.TikTok;
            if (string.IsNullOrEmpty(tiktokSettings.ClientKey) || string.IsNullOrEmpty(tiktokSettings.ClientSecret))
            {
                throw new AppConfigurationException("TikTok Client Key or Client Secret is not configured in settings.");
            }

            var options = new TikTokV2ClientOptions { ClientKey = tiktokSettings.ClientKey, ClientSecret = tiktokSettings.ClientSecret };
            var client = new TikTokV2Client(options);

            _logger.LogInformation("Initiating TikTok direct post for '{Title}'.", videoDetails.Title);

            var initRequest = new PostPublishVideoInitRequest()
            {
                AccessToken = _tokenData.AccessToken,
                PostInfo = new PostPublishVideoInitRequest.Types.PostInfo() { Title = videoDetails.Title },
                SourceInfo = new PostPublishVideoInitRequest.Types.SourceInfo() { Source = "FILE_UPLOAD" }
            };

            // This is an async method, but we are not awaiting it here.
            // This is a fire-and-forget operation as per the business logic.
            _ = Task.Run(async () =>
            {
                var initResponse = await client.ExecutePostPublishVideoInitAsync(initRequest, cancellationToken);

                if (!initResponse.IsSuccessful() || string.IsNullOrEmpty(initResponse.Data?.UploadUrl))
                {
                    throw new ApiException($"TikTok Error: Failed to initialize video upload. Description: {initResponse.ErrorDescription}");
                }

                _logger.LogInformation("TikTok upload initialized. Uploading video file to the provided URL...");

                byte[] videoBytes = await File.ReadAllBytesAsync(videoPath, cancellationToken);

                var httpResponseMessage = await new Flurl.Url(initResponse.Data.UploadUrl)
                    .WithHeader("Content-Type", "video/mp4")
                    .PutAsync(new ByteArrayContent(videoBytes), cancellationToken: cancellationToken);

                if (httpResponseMessage.StatusCode < 200 || httpResponseMessage.StatusCode >= 300)
                {
                    throw new ApiException($"Failed to upload video file to TikTok. Status: {httpResponseMessage.StatusCode}");
                }

                _logger.LogInformation("Video file successfully uploaded. TikTok will process it asynchronously. Publish ID: {PublishId}", initResponse.Data.PublishId);
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public Task SignOutAsync()
        {
            if (File.Exists(_tokenStorePath))
            {
                File.Delete(_tokenStorePath);
                _logger.LogInformation("TikTok token file deleted.");
            }
            _tokenData = null;
            return Task.CompletedTask;
        }

        public Task<bool> DoesVideoExistAsync(string _, CancellationToken __)
        {
            _logger.LogWarning("Checking for existing video by title is not supported by the TikTok API. Returning false.");
            return Task.FromResult(false);
        }

        public Task<HashSet<string>> GetUploadedVideoTitlesAsync(CancellationToken _)
        {
            _logger.LogWarning("Fetching uploaded video titles is not supported by the TikTok API. Returning an empty set.");
            return Task.FromResult(new HashSet<string>());
        }

        #region Private Helpers
        private async Task<HttpListenerContext> ListenForCallbackAsync(string redirectUrl, CancellationToken cancellationToken)
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUrl);
            try
            {
                listener.Start();

                var context = await listener.GetContextAsync().WaitAsync(cancellationToken);

                using var response = context.Response;
                const string responseString = "<html><body style='font-family: sans-serif;'><h1>Authentication successful!</h1><p>You can close this browser window now.</p></body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                await using var output = response.OutputStream;
                await output.WriteAsync(buffer, cancellationToken);

                return context;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Authentication was cancelled by the user.");
                throw;
            }
            finally
            {
                listener.Stop();
            }
        }

        private void SaveToken()
        {
            var json = JsonConvert.SerializeObject(_tokenData);
            File.WriteAllText(_tokenStorePath, json);
        }

        private void LoadToken()
        {
            if (File.Exists(_tokenStorePath))
            {
                var json = File.ReadAllText(_tokenStorePath);
                _tokenData = JsonConvert.DeserializeObject<TikTokTokenData>(json);
            }
        }

        private static string GenerateCodeVerifier()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string GenerateCodeChallenge(string codeVerifier)
        {
            var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Convert.ToBase64String(challengeBytes)
                .Replace('+', '-')
                .Replace('/', '_');
        }
        #endregion
    }
}