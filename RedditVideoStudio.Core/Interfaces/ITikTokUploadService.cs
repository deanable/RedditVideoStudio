using RedditVideoStudio.Domain.Models; // Add this using
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that uploads a video to TikTok.
    /// </summary>
    public interface ITikTokUploadService
    {
        /// <summary>
        /// Uploads a video to TikTok with the specified details.
        /// </summary>
        /// <param name="videoPath">The local path to the video file.</param>
        /// <param name="videoDetails">An object containing the title, description, and privacy settings for the video.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, CancellationToken cancellationToken = default);
    }
}