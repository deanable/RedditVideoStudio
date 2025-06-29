using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class WindowsTextToSpeechService : ITextToSpeechService
    {
        private readonly IAppConfiguration _appConfig;
        private readonly IFfmpegService _ffmpegService;
        private readonly IAudioUtility _audioUtility;
        private readonly ILogger<WindowsTextToSpeechService> _logger;

        public WindowsTextToSpeechService(IAppConfiguration appConfig, IFfmpegService ffmpegService, IAudioUtility audioUtility, ILogger<WindowsTextToSpeechService> logger)
        {
            _appConfig = appConfig;
            _ffmpegService = ffmpegService;
            _audioUtility = audioUtility;
            _logger = logger;
        }

        public string[] GetVoices()
        {
            using (var speechSynthesizer = new SpeechSynthesizer())
            {
                return speechSynthesizer.GetInstalledVoices()
                    .Where(v => v.Enabled)
                    .Select(v => v.VoiceInfo.Name)
                    .ToArray();
            }
        }

        // In: C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\WindowsSpeechSynthesizerService.cs

        public async Task<SpeechGenerationResult> GenerateSpeechAsync(string text, string outputFilePath, CancellationToken cancellationToken = default)
        {
            var tempWavPath = Path.ChangeExtension(outputFilePath, ".wav");
            var tcs = new TaskCompletionSource<bool>();

            using (var speechSynthesizer = new SpeechSynthesizer())
            {
                var voiceToUse = _appConfig.Settings.Tts.WindowsVoice;
                _logger.LogInformation("Using Windows TTS voice: \"{Voice}\"", voiceToUse);

                try
                {
                    speechSynthesizer.SelectVoice(voiceToUse);
                }
                catch (Exception ex)
                {
                    throw new TtsException($"Cannot set voice '{voiceToUse}'. No matching voice is installed or the voice was disabled.", ex);
                }

                speechSynthesizer.SetOutputToWaveFile(tempWavPath);
                _logger.LogInformation("Generating temporary WAV file at: \"{Path}\"", tempWavPath);

                speechSynthesizer.SpeakCompleted += (s, e) =>
                {
                    if (e.Error != null) tcs.TrySetException(e.Error);
                    else if (e.Cancelled) tcs.TrySetCanceled();
                    else tcs.TrySetResult(true);
                };

                using (cancellationToken.Register(() => speechSynthesizer.SpeakAsyncCancelAll()))
                {
                    speechSynthesizer.SpeakAsync(text);
                    await tcs.Task;
                }
            }

            // --- START OF CORRECTION ---

            // First, convert the WAV file to the final MP3 format.
            _logger.LogInformation("Converting '\"{WavPath}\"' to MP3 at '\"{Mp3Path}\"'.", tempWavPath, outputFilePath);
            await _ffmpegService.ConvertWavToMp3Async(tempWavPath, outputFilePath, cancellationToken);

            // Now, measure the duration of the FINAL MP3 file. This is the true duration.
            var accurateDuration = await _audioUtility.GetAudioDurationAsync(outputFilePath, cancellationToken);
            _logger.LogInformation("Accurate duration measured from MP3 file: {Duration}", accurateDuration);

            if (File.Exists(tempWavPath))
            {
                File.Delete(tempWavPath);
            }

            // Return the MP3 path along with its own accurate duration.
            return new SpeechGenerationResult(outputFilePath, accurateDuration);

            // --- END OF CORRECTION ---
        }
    }
}