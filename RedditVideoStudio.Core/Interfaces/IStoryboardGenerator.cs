using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that generates a storyboard from text content.
    /// This process involves creating audio and image assets for each piece of text.
    /// </summary>
    public interface IStoryboardGenerator
    {
        /// <summary>
        /// Asynchronously generates a storyboard from a collection of text strings.
        /// </summary>
        /// <param name="content">The collection of text strings to be included in the storyboard.</param>
        /// <param name="tempPath">The temporary directory to store generated assets.</param>
        /// <param name="segmentName">A name to identify the video segment (e.g., "Title", "Comments").</param>
        /// <param name="progress">An object to report progress on the generation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A Storyboard object populated with timed items.</returns>
        Task<Storyboard> GenerateAsync(
            IEnumerable<string> content,
            string tempPath,
            string segmentName,
            IProgress<ProgressReport> progress,
            CancellationToken cancellationToken);
    }
}
