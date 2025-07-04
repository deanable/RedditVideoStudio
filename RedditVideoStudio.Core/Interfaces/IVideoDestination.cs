// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Core\Interfaces\IVideoDestination.cs

namespace RedditVideoStudio.Core.Interfaces
{
    using RedditVideoStudio.Domain.Models;
    using System.Collections.Generic; // Required for HashSet
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a destination platform for publishing videos, like YouTube or TikTok.
    /// Defines the contract for authenticating, signing out, and uploading videos.
    /// </summary>
    public interface IVideoDestination
    {
        /// <summary>
        /// Gets the unique name of the destination (e.g., "YouTube").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a value indicating whether the service is currently authenticated.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Initiates the authentication process for the destination.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task AuthenticateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a video to the destination platform.
        /// </summary>
        /// <param name="videoPath">The local file path of the video to upload.</param>
        /// <param name="videoDetails">The metadata for the video (title, description, etc.).</param>
        /// <param name="thumbnailPath">The local file path of the video's thumbnail.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, string? thumbnailPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs the user out from the destination platform and revokes credentials.
        /// </summary>
        Task SignOutAsync();

        /// <summary>
        /// Checks if a video with the specified title already exists on the destination platform.
        /// </summary>
        /// <param name="title">The title of the video to check for.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True if a video with the exact title exists, otherwise false.</returns>
        Task<bool> DoesVideoExistAsync(string title, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a collection of all video titles that have been uploaded to the destination.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A HashSet containing the titles of all uploaded videos.</returns>
        Task<HashSet<string>> GetUploadedVideoTitlesAsync(CancellationToken cancellationToken = default);
    }
}