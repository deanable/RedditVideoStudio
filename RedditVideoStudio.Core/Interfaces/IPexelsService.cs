using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that interacts with the Pexels API
    /// to download stock photos and videos.
    /// </summary>
    public interface IPexelsService
    {
        /// <summary>
        /// Downloads a random video based on a search query.
        /// </summary>
        /// <param name="query">The search term for the video.</param>
        /// <param name="downloadPath">The local path to save the downloaded video.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The path to the downloaded video file.</returns>
        Task<string> DownloadRandomVideoAsync(string query, string downloadPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a random image based on a search query.
        /// </summary>
        /// <param name="query">The search term for the image.</param>
        /// <param name="downloadPath">The local path to save the downloaded image.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The path to the downloaded image file.</returns>
        Task<string> DownloadRandomImageAsync(string query, string downloadPath, CancellationToken cancellationToken = default);
    }
}
