using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Shared.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that generates a single video segment
    /// from a storyboard and a background video.
    /// </summary>
    public interface IVideoSegmentGenerator
    {
        /// <summary>
        /// Asynchronously generates a video segment.
        /// </summary>
        /// <param name="storyboard">The storyboard containing the sequence of items for the segment.</param>
        /// <param name="backgroundQuery">The search query for the background video.</param>
        /// <param name="tempPath">The temporary directory to store intermediate files.</param>
        /// <param name="segmentName">A name to identify the video segment.</param>
        /// <param name="outputSegmentPath">The path to save the final rendered video segment.</param>
        /// <param name="progress">An object to report progress on the generation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The path to the generated video segment.</returns>
        Task<string> GenerateAsync(
            Storyboard storyboard,
            string backgroundQuery,
            string tempPath,
            string segmentName,
            string outputSegmentPath,
            IProgress<ProgressReport> progress,
            CancellationToken cancellationToken);
    }
}
