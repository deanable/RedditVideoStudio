namespace RedditVideoStudio.Core.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RedditVideoStudio.Domain.Models;
    using RedditVideoStudio.Shared.Models; // Required for IProgress<ProgressReport>

    /// <summary>
    /// Defines the contract for a service that orchestrates the publishing of videos
    /// to multiple destinations.
    /// </summary>
    public interface IPublishingService
    {
        /// <summary>
        /// Composes and uploads a video to the specified destinations.
        /// </summary>
        /// <param name="post">The Reddit post data to generate the video from.</param>
        /// <param name="destinationNames">A list of destination names (e.g., "YouTube", "TikTok") to publish to.</param>
        /// <param name="progress">An object to report progress on the operation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task PublishVideoAsync(RedditPostData post, IEnumerable<string> destinationNames, IProgress<ProgressReport> progress, CancellationToken cancellationToken);
    }
}
