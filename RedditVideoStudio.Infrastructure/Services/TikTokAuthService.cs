// File: C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\TikTokAuthService.cs

using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net;

namespace RedditVideoStudio.Infrastructure.Services
{
    // ... (ITikTokAuthService interface and other records remain the same) ...
    public record TikTokApiConfig
    {
        public string ClientKey { get; init; } = string.Empty;
        public string ClientSecret { get; init; } = string.Empty;
        public string RedirectUri { get; init; } = string.Empty;
        public string Scopes { get; init; } = string.Empty;
    }

    public interface ITikTokAuthService
    {
        string GetAuthorizationUrl(string state);
        Task<TikTokTokenResponse> ExchangeCodeForTokensAsync(string code, string state, string storedState);
        Task<TikTokTokenResponse> RefreshAccessTokenAsync(string refreshToken);
    }

    /// <summary>
    /// Implements the logic for interacting with the TikTok OAuth 2.0 v2 API endpoints.
    /// </summary>
    public class TikTokAuthService : ITikTokAuthService
    {
        private const string AuthorizationEndpoint = "https://www.tiktok.com/v2/auth/authorize/";
        private const string TokenEndpoint = "https://open.tiktokapis.com/v2/oauth/token/";

        private readonly HttpClient _httpClient;
        private readonly TikTokApiConfig _config;
        private readonly ILogger<TikTokAuthService> _logger;

        public TikTokAuthService(
            System.Net.Http.IHttpClientFactory httpClientFactory,
            IAppConfiguration config,
            ILogger<TikTokAuthService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("TikTokApiClient");
            _logger = logger;

            _config = new TikTokApiConfig
            {
                ClientKey = config.Settings.TikTok.ClientKey ?? throw new ArgumentNullException(nameof(config.Settings.TikTok.ClientKey)),
                ClientSecret = config.Settings.TikTok.ClientSecret ?? throw new ArgumentNullException(nameof(config.Settings.TikTok.ClientSecret)),
                RedirectUri = config.Settings.TikTok.RedirectUri ?? throw new ArgumentNullException(nameof(config.Settings.TikTok.RedirectUri)),
                Scopes = config.Settings.TikTok.Scopes
            };
        }

        /// <inheritdoc />
        public string GetAuthorizationUrl(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException("State parameter cannot be empty.", nameof(state));
            }

            var queryParams = new Dictionary<string, string>
            {
                { "client_key", _config.ClientKey },
                { "scope", _config.Scopes },
                { "response_type", "code" },
                { "redirect_uri", _config.RedirectUri },
                { "state", state }
            };

            var queryString = string.Join("&", queryParams.Select(kvp =>
                $"{kvp.Key}={WebUtility.UrlEncode(kvp.Value)}"));

            var fullUrl = $"{AuthorizationEndpoint}?{queryString}";
            _logger.LogInformation("Generated TikTok Authorization URL: {Url}", fullUrl);
            return fullUrl;
        }

        /// <inheritdoc />
        public async Task<TikTokTokenResponse> ExchangeCodeForTokensAsync(string code, string state, string storedState)
        {
            if (string.IsNullOrEmpty(state) || state != storedState)
            {
                _logger.LogWarning("Invalid state parameter received. Possible CSRF attack detected.");
                throw new SecurityException("Invalid state parameter. Possible CSRF attack.");
            }

            // CORRECTED: Do NOT manually decode the authorization code.
            // The FormUrlEncodedContent will handle the necessary encoding of the raw code string.
            // Manually decoding it first can lead to the "double encoding" issue mentioned in the docs.
            // var decodedCode = WebUtility.UrlDecode(code); // This line has been removed.

            var requestBody = new Dictionary<string, string>
            {
                { "client_key", _config.ClientKey },
                { "client_secret", _config.ClientSecret },
                { "code", code }, // Pass the raw code directly.
                { "grant_type", "authorization_code" },
                { "redirect_uri", _config.RedirectUri }
            };

            return await PostToTokenEndpointAsync(requestBody);
        }

        // ... (The rest of the file remains the same) ...

        public async Task<TikTokTokenResponse> RefreshAccessTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ArgumentException("Refresh token cannot be empty.", nameof(refreshToken));
            }

            _logger.LogInformation("Attempting to refresh TikTok access token.");
            var requestBody = new Dictionary<string, string>
            {
                { "client_key", _config.ClientKey },
                { "client_secret", _config.ClientSecret },
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken }
            };

            return await PostToTokenEndpointAsync(requestBody);
        }

        private async Task<TikTokTokenResponse> PostToTokenEndpointAsync(Dictionary<string, string> requestBody)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(requestBody)
                };

                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = JsonSerializer.Deserialize<TikTokErrorResponse>(responseContent);
                    _logger.LogError("Error from TikTok API: {Error} - {Description} (Log ID: {LogId})",
                        errorResponse?.Error, errorResponse?.ErrorDescription, errorResponse?.LogId);
                    throw new HttpRequestException(
                        $"Error from TikTok API: {errorResponse?.Error} - {errorResponse?.ErrorDescription} (Log ID: {errorResponse?.LogId})",
                        null,
                        response.StatusCode);
                }

                _logger.LogInformation("Successfully received token response from TikTok.");
                var tokenResponse = JsonSerializer.Deserialize<TikTokTokenResponse>(responseContent);
                return tokenResponse ?? throw new InvalidOperationException("Failed to deserialize token response.");
            }
            catch (Exception ex) when (ex is not HttpRequestException)
            {
                _logger.LogError(ex, "An unexpected error occurred while posting to the TikTok token endpoint.");
                throw;
            }
        }
    }

    public record TikTokTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; init; }
        [JsonPropertyName("open_id")]
        public string OpenId { get; init; } = string.Empty;
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;
        [JsonPropertyName("refresh_expires_in")]
        public long RefreshExpiresIn { get; init; }
        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;
        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;
    }

    public record TikTokErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; init; } = string.Empty;
        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; init; } = string.Empty;
        [JsonPropertyName("log_id")]
        public string LogId { get; init; } = string.Empty;
    }
}