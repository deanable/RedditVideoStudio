using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the IPexelsService interface to interact with the Pexels API
    /// for downloading stock photos and videos.
    /// </summary>
    public class PexelsService : IPexelsService
    {
        private readonly HttpClient _client;
        private readonly ILogger<PexelsService> _logger;
        private readonly IAppConfiguration _appConfig;

        public PexelsService(IAppConfiguration appConfig, ILogger<PexelsService> logger)
        {
            _logger = logger;
            _appConfig = appConfig;

            var apiKey = _appConfig.Settings.Pexels.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new AppConfigurationException("Pexels API key is missing or empty in configuration.");
            }

            _client = new HttpClient
            {
                BaseAddress = new Uri("https://api.pexels.com/")
            };
            _client.DefaultRequestHeaders.Add("Authorization", apiKey);
        }

        public async Task<string> DownloadRandomVideoAsync(string query, string downloadPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = $"videos/search?query={Uri.EscapeDataString(query)}&per_page=15&orientation=landscape";
                var response = await _client.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new PexelsApiException($"Pexels video API request failed with status {response.StatusCode}. Content: {errorContent}");
                }

                using var json = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(json, cancellationToken: cancellationToken);
                var videos = document.RootElement.GetProperty("videos");

                if (videos.GetArrayLength() == 0)
                {
                    throw new PexelsApiException($"No videos found for query '{query}'");
                }

                var randomVideo = videos[new Random().Next(videos.GetArrayLength())];
                var videoLink = randomVideo.GetProperty("video_files")[0].GetProperty("link").GetString();

                if (string.IsNullOrEmpty(videoLink))
                {
                    throw new PexelsApiException("Could not find a downloadable video link in the Pexels API response.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(downloadPath)!);

                using var videoStream = await _client.GetStreamAsync(videoLink, cancellationToken);
                await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await videoStream.CopyToAsync(fileStream, cancellationToken);

                _logger.LogInformation("Pexels video saved to: {Path}", downloadPath);
                return downloadPath;
            }
            catch (Exception ex) when (ex is not PexelsApiException)
            {
                _logger.LogError(ex, "An unexpected error occurred while downloading a video from Pexels for query: {Query}", query);
                throw new PexelsApiException($"An unexpected error occurred while downloading from Pexels: {ex.Message}", ex);
            }
        }

        public async Task<string> DownloadRandomImageAsync(string query, string downloadPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUrl = $"v1/search?query={Uri.EscapeDataString(query)}&per_page=1";
                var response = await _client.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new PexelsApiException($"Pexels image API request failed with status {response.StatusCode}. Content: {errorContent}");
                }

                using var json = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(json, cancellationToken: cancellationToken);
                var imageLink = document.RootElement.GetProperty("photos")[0].GetProperty("src").GetProperty("original").GetString();

                if (string.IsNullOrEmpty(imageLink))
                {
                    throw new PexelsApiException("Could not find a downloadable image link in the Pexels API response.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(downloadPath)!);

                using var imageStream = await _client.GetStreamAsync(imageLink, cancellationToken);
                await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await imageStream.CopyToAsync(fileStream, cancellationToken);

                _logger.LogInformation("Pexels image saved to: {Path}", downloadPath);
                return downloadPath;
            }
            catch (Exception ex) when (ex is not PexelsApiException)
            {
                _logger.LogError(ex, "An unexpected error occurred while downloading an image from Pexels for query: {Query}", query);
                throw new PexelsApiException($"An unexpected error occurred while downloading from Pexels: {ex.Message}", ex);
            }
        }
    }
}
