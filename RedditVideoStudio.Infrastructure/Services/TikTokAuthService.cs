namespace RedditVideoStudio.Infrastructure.Services
{
    using Microsoft.Extensions.Logging;
    using RedditVideoStudio.Core.Exceptions;
    using RedditVideoStudio.Core.Interfaces;
    using RedditVideoStudio.Shared.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

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
            string redirectUri = "http://localhost:8912/callback/";

            var authUrl = "https://www.tiktok.com/v2/auth/authorize/" +
                          $"?client_key={_settings.TikTok.ClientKey}" +
                          $"&scope={_settings.TikTok.Scopes}" +
                          "&response_type=code" +
                           $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                          $"&state={state}" +
                          $"&code_challenge={codeChallenge}" +
                          "&code_challenge_method=S256";

            // ADDED: Log the full authorization URL for debugging
            _logger.LogInformation("Opening browser for TikTok authorization. Full URL: {AuthUrl}", authUrl);

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            string authCode = await ListenForCallback(redirectUri, state, cancellationToken);

            _logger.LogInformation("Received authorization code, exchanging for access token...");
            return await ExchangeCodeForToken(authCode, codeVerifier, cancellationToken);
        }

        private async Task<string> ListenForCallback(string redirectUri, string expectedState, CancellationToken cancellationToken)
        {
            try // ADDED: Try/catch block for listener errors
            {
                using var listener = new HttpListener();
                listener.Prefixes.Add(redirectUri);
                listener.Start();
                _logger.LogInformation("HttpListener started. Waiting for authentication callback on {Uri}", redirectUri);

                var context = await listener.GetContextAsync();
                _logger.LogInformation("Request received by listener.");

                var request = context.Request;
                // ADDED: Log the full incoming request URL
                _logger.LogDebug("Listener received request for URL: {RequestUrl}", request.Url?.ToString() ?? "null");

                string? code = request.QueryString.Get("code");
                string? incomingState = request.QueryString.Get("state");
                string? error = request.QueryString.Get("error");
                string? errorDescription = request.QueryString.Get("error_description");

                // ADDED: Log received parameters
                _logger.LogInformation("Received callback parameters. Code: '{Code}', State: '{State}', Error: '{Error}', ErrorDescription: '{ErrorDescription}'",
                    code ?? "null", incomingState ?? "null", error ?? "null", errorDescription ?? "null");

                var response = context.Response;
                string responseString = "<html><body><h1>Authentication successful!</h1><p>You can close this browser window now.</p></body></html>";
                var buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                var output = response.OutputStream;
                await output.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                output.Close();
                listener.Stop();
                _logger.LogInformation("Listener has stopped.");

                if (!string.IsNullOrEmpty(error))
                {
                    throw new ApiException($"TikTok returned an error: {error} - {errorDescription}");
                }

                if (string.IsNullOrEmpty(code) || incomingState != expectedState)
                {
                    throw new ApiException("TikTok authorization failed. State parameter mismatch or no code received.");
                }
                return code;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The local HTTP listener failed.");
                throw;
            }
        }

        // ... (rest of the file remains the same) ...
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
                var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
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