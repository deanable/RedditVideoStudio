using System;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a utility service that provides
    /// audio-related functionalities.
    /// </summary>
    public interface IAudioUtility
    {
        /// <summary>
        /// Asynchronously gets the duration of an audio file.
        /// </summary>
        /// <param name="audioFilePath">The path to the audio file.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The duration of the audio file as a TimeSpan.</returns>
        Task<TimeSpan> GetAudioDurationAsync(string audioFilePath, CancellationToken cancellationToken = default);
    }
}
