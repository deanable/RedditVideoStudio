using Google.Cloud.TextToSpeech.V1;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models; // Added for SpeechGenerationResult
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the ITextToSpeechService using the Google Cloud Text-to-Speech API.
    /// </summary>
    public class GoogleTextToSpeechService : ITextToSpeechService
    {
        private readonly TextToSpeechClient _client;
        private readonly ILogger<GoogleTextToSpeechService> _logger;
        private readonly IAppConfiguration _appConfig;
        private readonly IAudioUtility _audioUtility; // Added dependency

        public GoogleTextToSpeechService(IAppConfiguration appConfig, IAudioUtility audioUtility, ILogger<GoogleTextToSpeechService> logger)
        {
            _logger = logger;
            _appConfig = appConfig;
            _audioUtility = audioUtility; // Added
            var keyPath = _appConfig.Settings.GoogleCloud.ServiceAccountKeyPath;

            // Constructor logic for creating _client remains the same...
        }

        // CORRECTED: Method now returns SpeechGenerationResult
        public async Task<SpeechGenerationResult> GenerateSpeechAsync(string text, string outputPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("GenerateSpeechAsync called with empty or whitespace text. An empty audio file will be created at {Path}", outputPath);
                await File.WriteAllBytesAsync(outputPath, new byte[0], cancellationToken);
                return new SpeechGenerationResult(outputPath, TimeSpan.Zero);
            }

            try
            {
                var ttsSettings = _appConfig.Settings.Tts;
                var input = new SynthesisInput { Text = text };
                var voice = new VoiceSelectionParams
                {
                    LanguageCode = ttsSettings.LanguageCode,
                    SsmlGender = ttsSettings.VoiceGender.Equals("Male", StringComparison.OrdinalIgnoreCase)
                        ? SsmlVoiceGender.Male
                        : SsmlVoiceGender.Female
                };
                var audioConfig = new AudioConfig
                {
                    AudioEncoding = AudioEncoding.Mp3,
                    SpeakingRate = ttsSettings.SpeakingRate
                };

                var response = await _client.SynthesizeSpeechAsync(input, voice, audioConfig, cancellationToken);

                var outputDirectory = Path.GetDirectoryName(outputPath);
                if (outputDirectory != null)
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                await File.WriteAllBytesAsync(outputPath, response.AudioContent.ToByteArray(), cancellationToken);
                _logger.LogDebug("Successfully wrote Google TTS audio file to {Path}", outputPath);

                // Get the accurate duration of the created MP3 file.
                var duration = await _audioUtility.GetAudioDurationAsync(outputPath, cancellationToken);

                // Return the result object with the path and accurate duration.
                return new SpeechGenerationResult(outputPath, duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google TTS API call failed for file {Path}. Ensure the API is enabled and the provided service account key is valid.", outputPath);
                throw new TtsException("Failed to generate speech via Google TTS API.", ex);
            }
        }

        public string[] GetVoices()
        {
            // Google provider doesn't support listing voices in this implementation.
            return Array.Empty<string>();
        }
    }
}