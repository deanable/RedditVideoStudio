using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class TikTokAuthService
    {
        private readonly ILogger<TikTokAuthService> _logger;
        private readonly AppSettings _settings;
        private readonly HttpClient _httpClient;

        public TikTokAuthService(ILogger<TikTokAuthService> logger, IAppConfiguration config)
        {
            _logger = logger;
            _settings = config.Settings;
            _httpClient = new HttpClient();
        }

        public async Task<string> AuthorizeAndGetTokenAsync(CancellationToken cancellationToken)
        {
            string codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(codeVerifier);
            string state = Guid.NewGuid().ToString();

            var authUrl = "https://www.tiktok.com/v2/auth/authorize/" +
                          $"?client_key={_settings.TikTok.ClientKey}" +
                          $"&scope={_settings.TikTok.Scopes}" +
                          "&response_type=code" +
                          $"&redirect_uri={HttpUtility.UrlEncode("http://localhost:8912/callback/")}" +
                          $"&state={state}" +
                          $"&code_challenge={codeChallenge}" +
                          "&code_challenge_method=S256";

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            string? callbackUrl = ShowUrlInputDialog();
            if (string.IsNullOrWhiteSpace(callbackUrl))
            {
                throw new OperationCanceledException("TikTok authentication was canceled.");
            }

            var queryParams = HttpUtility.ParseQueryString(new Uri(callbackUrl).Query);
            string? authCode = queryParams.Get("code");
            string? incomingState = queryParams.Get("state");

            if (string.IsNullOrEmpty(authCode) || incomingState != state)
            {
                throw new ApiException("TikTok authorization failed. State parameter mismatch indicates a possible CSRF attack.");
            }

            _logger.LogInformation("Received authorization code, exchanging for access token...");
            return await ExchangeCodeForToken(authCode, codeVerifier, cancellationToken);
        }

        private async Task<string> ExchangeCodeForToken(string authCode, string codeVerifier, CancellationToken cancellationToken)
        {
            var requestBody = new Dictionary<string, string>
            {
                { "client_key", _settings.TikTok.ClientKey ?? "" },
                { "client_secret", _settings.TikTok.ClientSecret ?? "" },
                { "code", authCode },
                { "grant_type", "authorization_code" },
                { "redirect_uri", "http://localhost:8912/callback/" },
                { "code_verifier", codeVerifier }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/oauth/token/");
            request.Content = new FormUrlEncodedContent(requestBody);

            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogInformation("Raw TikTok token response: {Response}", responseString);

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = JsonSerializer.Deserialize<TikTokErrorResponse>(responseString);
                    throw new ApiException($"TikTok API Error: {errorResponse?.Error?.ErrorDescription} (Log ID: {errorResponse?.Error?.LogId})");
                }

                var tokenResponse = JsonSerializer.Deserialize<TikTokTokenResponse>(responseString);
                if (string.IsNullOrEmpty(tokenResponse?.Data?.AccessToken))
                {
                    throw new ApiException("Could not find access_token in the response from TikTok.");
                }

                _logger.LogInformation("Successfully obtained TikTok access token.");
                return tokenResponse.Data.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while exchanging the code for an access token.");
                throw new ApiException("An unexpected error occurred during the token exchange.", ex);
            }
        }

        private string? ShowUrlInputDialog()
        {
            using var form = new Form
            {
                Text = "Paste Callback URL",
                Size = new Size(500, 180),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                TopMost = true
            };

            var label = new Label() { Left = 20, Top = 20, Width = 440, Text = "After authorizing in your browser, copy the full URL from the address bar (it will show an error page) and paste it below." };
            var textBox = new TextBox() { Left = 20, Top = 60, Width = 440 };
            var buttonOk = new Button() { Text = "OK", Left = 300, Width = 100, Top = 90, DialogResult = DialogResult.OK };
            var buttonCancel = new Button() { Text = "Cancel", Left = 40, Width = 100, Top = 90, DialogResult = DialogResult.Cancel };

            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }

        private string GenerateCodeVerifier()
        {
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                return Convert.ToBase64String(challengeBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            }
        }

        private record TikTokTokenResponse
        {
            [JsonPropertyName("data")]
            public TikTokTokenData? Data { get; init; }
        }

        private record TikTokTokenData
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; init; } = "";
        }

        private record TikTokErrorResponse
        {
            [JsonPropertyName("error")]
            public TikTokErrorData? Error { get; init; }
        }

        private record TikTokErrorData
        {
            [JsonPropertyName("code")]
            public string Code { get; init; } = "";

            [JsonPropertyName("message")]
            public string ErrorDescription { get; init; } = "";

            [JsonPropertyName("log_id")]
            public string LogId { get; init; } = "";
        }
    }
}