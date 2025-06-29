using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models; // Added
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the ITextToSpeechService using the Azure Cognitive Services Speech SDK.
    /// </summary>
    public class AzureTextToSpeechService : ITextToSpeechService
    {
        private readonly ILogger<AzureTextToSpeechService> _logger;
        private readonly SpeechConfig _speechConfig;
        private readonly IAudioUtility _audioUtility; // Added

        // CORRECTED: Constructor now accepts IAudioUtility
        public AzureTextToSpeechService(ILogger<AzureTextToSpeechService> logger, IAppConfiguration configService, IAudioUtility audioUtility)
        {
            _logger = logger;
            _audioUtility = audioUtility; // Added
            var azureSettings = configService.Settings.AzureTts;

            if (string.IsNullOrWhiteSpace(azureSettings.ApiKey) || string.IsNullOrWhiteSpace(azureSettings.Region))
            {
                _logger.LogError("Azure TTS API Key or Region is not configured.");
                // Create a "null" config to prevent crashes, but it will fail on use
                _speechConfig = SpeechConfig.FromSubscription("missing", "missing");
            }
            else
            {
                _speechConfig = SpeechConfig.FromSubscription(azureSettings.ApiKey, azureSettings.Region);
                _speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3);
            }
        }

        // CORRECTED: Method now returns the SpeechGenerationResult record
        public async Task<SpeechGenerationResult> GenerateSpeechAsync(string text, string outputFilePath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating speech using Azure TTS for text snippet: '{TextSnippet}'", text.Length > 40 ? text[..40] + "..." : text);

            try
            {
                var outputDirectory = Path.GetDirectoryName(outputFilePath);
                if (outputDirectory != null)
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                using var synthesizer = new SpeechSynthesizer(_speechConfig, null);
                var result = await synthesizer.SpeakTextAsync(text);

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Azure TTS generation was cancelled.");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                    _logger.LogError("Azure TTS synthesis canceled. Reason: {Reason}, ErrorCode: {ErrorCode}, Details: {ErrorDetails}",
                        cancellation.Reason, cancellation.ErrorCode, cancellation.ErrorDetails);
                    throw new TtsException($"Azure TTS synthesis failed: {cancellation.ErrorDetails}");
                }

                if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                {
                    await File.WriteAllBytesAsync(outputFilePath, result.AudioData, cancellationToken);
                    _logger.LogInformation("Successfully wrote Azure TTS audio file to {Path}", outputFilePath);

                    // Get the accurate duration of the created audio file.
                    var duration = await _audioUtility.GetAudioDurationAsync(outputFilePath, cancellationToken);

                    // Return the new result object containing the path and the duration.
                    return new SpeechGenerationResult(outputFilePath, duration);
                }

                throw new TtsException($"Azure TTS synthesis returned an unexpected status: {result.Reason}");
            }
            catch (Exception ex) when (ex is not TtsException)
            {
                _logger.LogError(ex, "An unexpected error occurred during Azure TTS speech generation for file {Path}", outputFilePath);
                throw new TtsException("An unexpected error occurred during Azure TTS speech generation.", ex);
            }
        }

        public string[] GetVoices()
        {
            // Azure provider doesn't support listing voices in this implementation.
            return Array.Empty<string>();
        }
    }
}