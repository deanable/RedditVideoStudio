using RedditVideoStudio.Domain.Models; // Added
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    public interface ITextToSpeechService
    {
        /// <summary>
        /// Asynchronously generates speech and returns the file path and its accurate duration.
        /// </summary>
        // CORRECTED: Return type is now the new SpeechGenerationResult record.
        Task<SpeechGenerationResult> GenerateSpeechAsync(string text, string outputFilePath, CancellationToken cancellationToken = default);

        string[] GetVoices();
    }
}