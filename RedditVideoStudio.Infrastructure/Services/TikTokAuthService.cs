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
    /// <summary>
    /// Represents the configuration for the TikTok API, loaded from AppSettings.
    /// This is a sample record to show how configuration can be structured.
    /// </summary>
    public record TikTokApiConfig
    {
        public string ClientKey { get; init; } = string.Empty;
        public string ClientSecret { get; init; } = string.Empty;
        public string RedirectUri { get; init; } = string.Empty;
        public string Scopes { get; init; } = string.Empty;
    }

    /// <summary>
    /// Defines the contract for a service that handles TikTok OAuth 2.0 authorization.
    /// </summary>
    public interface ITikTokAuthService
    {
        /// <summary>
        /// Generates the TikTok authorization URL to which the user should be redirected.
        /// </summary>
        /// <param name="state">A cryptographically secure random string to prevent CSRF attacks.</param>
        /// <returns>The full authorization URL.</returns>
        string GetAuthorizationUrl(string state);

        /// <summary>
        /// Exchanges an authorization code for an access token and refresh token.
        /// </summary>
        /// <param name="code">The authorization code from the TikTok callback.</param>
        /// <param name="state">The state value from the TikTok callback.</param>
        /// <param name="storedState">The state value that was stored in the user's session prior to the redirect.</param>
        /// <returns>A <see cref="TikTokTokenResponse"/> containing the tokens.</returns>
        Task<TikTokTokenResponse> ExchangeCodeForTokensAsync(string code, string state, string storedState);

        /// <summary>
        /// Refreshes an expired access token using a valid refresh token.
        /// </summary>
        /// <param name="refreshToken">The long-lived refresh token.</param>
        /// <returns>A new <see cref="TikTokTokenResponse"/> containing the refreshed tokens.</returns>
        Task<TikTokTokenResponse> RefreshAccessTokenAsync(string refreshToken);
    }

    /// <summary>
    /// Implements the logic for interacting with the TikTok OAuth 2.0 v2 API endpoints.
    /// This service is designed for a standard web application flow as described in the documentation.
    /// </summary>
    public class TikTokAuthService : ITikTokAuthService
    {
        // Defines the constant endpoints for the TikTok v2 API.
        private const string AuthorizationEndpoint = "https://www.tiktok.com/v2/auth/authorize/";
        private const string TokenEndpoint = "https://open.tiktokapis.com/v2/oauth/token/";

        private readonly HttpClient _httpClient;
        private readonly TikTokApiConfig _config;
        private readonly ILogger<TikTokAuthService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TikTokAuthService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Factory to create HttpClient instances.</param>
        /// <param name="config">The application's configuration service to get TikTok settings.</param>
        /// <param name="logger">The logger for logging information and errors.</param>
        public TikTokAuthService(
            // CORRECTED: Fully qualify the type to resolve the ambiguity.
            System.Net.Http.IHttpClientFactory httpClientFactory,
            IAppConfiguration config,
            ILogger<TikTokAuthService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("TikTokApiClient");
            _logger = logger;

            // Load configuration from the main AppSettings object.
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

            // A dictionary is used to hold all the query parameters for the authorization URL.
            var queryParams = new Dictionary<string, string>
            {
                { "client_key", _config.ClientKey },
                { "scope", _config.Scopes },
                { "response_type", "code" }, // This must always be "code" for this flow.
                { "redirect_uri", _config.RedirectUri },
                { "state", state } // The state parameter is crucial for CSRF protection.
            };

            // Join the parameters into a URL-encoded query string.
            var queryString = string.Join("&", queryParams.Select(kvp =>
                $"{kvp.Key}={WebUtility.UrlEncode(kvp.Value)}"));

            var fullUrl = $"{AuthorizationEndpoint}?{queryString}";
            _logger.LogInformation("Generated TikTok Authorization URL: {Url}", fullUrl);
            return fullUrl;
        }

        /// <inheritdoc />
        public async Task<TikTokTokenResponse> ExchangeCodeForTokensAsync(string code, string state, string storedState)
        {
            // First, validate the state parameter to prevent CSRF attacks.
            if (string.IsNullOrEmpty(state) || state != storedState)
            {
                _logger.LogWarning("Invalid state parameter received. Possible CSRF attack detected.");
                throw new SecurityException("Invalid state parameter. Possible CSRF attack.");
            }

            // Per TikTok documentation, the authorization code may need to be URL-decoded.
            var decodedCode = WebUtility.UrlDecode(code);

            // The body of the POST request to the token endpoint.
            var requestBody = new Dictionary<string, string>
            {
                { "client_key", _config.ClientKey },
                { "client_secret", _config.ClientSecret }, // The client secret is mandatory for this flow.
                { "code", decodedCode },
                { "grant_type", "authorization_code" }, // The grant_type must be 'authorization_code'.
                { "redirect_uri", _config.RedirectUri }
            };

            return await PostToTokenEndpointAsync(requestBody);
        }

        /// <inheritdoc />
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
                { "grant_type", "refresh_token" }, // The grant_type must be 'refresh_token'.
                { "refresh_token", refreshToken }
            };

            return await PostToTokenEndpointAsync(requestBody);
        }

        /// <summary>
        /// A private helper method to handle the POST request to the token endpoint and process the response.
        /// </summary>
        private async Task<TikTokTokenResponse> PostToTokenEndpointAsync(Dictionary<string, string> requestBody)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    // The content of the request must be form URL encoded.
                    Content = new FormUrlEncodedContent(requestBody)
                };

                // The TikTok API explicitly requires this Content-Type header.
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                // If the response is not successful, deserialize the error and throw an exception.
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
                // Deserialize the successful response into our DTO.
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

    /// <summary>
    /// Represents a successful token response from the TikTok OAuth API.
    /// This DTO maps directly to the JSON response structure.
    /// </summary>
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

    /// <summary>
    /// Represents an error response from the TikTok OAuth API.
    /// This DTO captures the structured error information provided by TikTok.
    /// </summary>
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