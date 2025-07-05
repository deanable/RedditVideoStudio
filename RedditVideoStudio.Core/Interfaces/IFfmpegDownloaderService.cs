using System;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that ensures FFmpeg is available for the application to use,
    /// downloading and extracting it if necessary.
    /// </summary>
    public interface IFfmpegDownloaderService
    {
        /// <summary>
        /// Checks for the presence of FFmpeg and downloads it if it is not found.
        /// </summary>
        /// <param name="progress">An object to report progress messages during the operation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EnsureFfmpegIsAvailableAsync(IProgress<string> progress, CancellationToken cancellationToken);
    }
}