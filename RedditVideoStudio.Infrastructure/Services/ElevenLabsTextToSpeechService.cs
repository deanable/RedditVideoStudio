using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models; // Added
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the ITextToSpeechService using the ElevenLabs API. 
    /// </summary>
    public class ElevenLabsTextToSpeechService : ITextToSpeechService
    {
        private readonly HttpClient _httpClient;
        private readonly IAppConfiguration _appConfig;
        private readonly ILogger<ElevenLabsTextToSpeechService> _logger;
        private readonly IAudioUtility _audioUtility; // Added

        // CORRECTED: Constructor now accepts IAudioUtility
        public ElevenLabsTextToSpeechService(IAppConfiguration appConfig, IAudioUtility audioUtility, ILogger<ElevenLabsTextToSpeechService> logger)
        {
            _appConfig = appConfig;
            _logger = logger;
            _audioUtility = audioUtility; // Added
            _httpClient = new HttpClient
            {
                BaseAddress = new System.Uri("https://api.elevenlabs.io/")
            };
        }

        // CORRECTED: Method now returns the SpeechGenerationResult record
        public async Task<SpeechGenerationResult> GenerateSpeechAsync(string text, string outputFilePath, CancellationToken cancellationToken = default)
        {
            var settings = _appConfig.Settings.ElevenLabs;
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new AppConfigurationException("ElevenLabs API key is not configured.");
            }
            if (string.IsNullOrWhiteSpace(settings.VoiceId))
            {
                throw new AppConfigurationException("ElevenLabs VoiceId is not configured.");
            }

            _logger.LogInformation("Generating speech using ElevenLabs TTS for text snippet: '{TextSnippet}'", text.Length > 40 ? text[..40] + "..." : text);

            try
            {
                var requestUrl = $"v1/text-to-speech/{settings.VoiceId}";
                var requestBody = new
                {
                    text,
                    model_id = settings.ModelId,
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("xi-api-key", settings.ApiKey);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("ElevenLabs API request failed with status {StatusCode}. Content: {ErrorContent}", response.StatusCode, errorContent);
                    throw new ElevenLabsApiException($"ElevenLabs API request failed: {response.ReasonPhrase}. Details: {errorContent}");
                }

                var outputDirectory = Path.GetDirectoryName(outputFilePath);
                if (outputDirectory != null)
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                byte[] audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                await File.WriteAllBytesAsync(outputFilePath, audioData, cancellationToken);

                _logger.LogDebug("Successfully wrote ElevenLabs TTS audio file to {Path}", outputFilePath);

                // Get the accurate duration of the created audio file.
                var duration = await _audioUtility.GetAudioDurationAsync(outputFilePath, cancellationToken);

                // Return the new result object containing the path and the duration.
                return new SpeechGenerationResult(outputFilePath, duration);
            }
            catch (Exception ex) when (ex is not ElevenLabsApiException)
            {
                _logger.LogError(ex, "An unexpected error occurred during ElevenLabs speech generation for file {Path}", outputFilePath);
                throw new TtsException("An unexpected error occurred during ElevenLabs speech generation.", ex);
            }
        }

        public string[] GetVoices()
        {
            // ElevenLabs provider doesn't support listing voices in this implementation.
            return Array.Empty<string>();
        }
    }
}