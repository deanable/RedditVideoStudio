using RedditVideoStudio.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for the main video composition service.
    /// This service orchestrates the entire process of creating a video
    /// from Reddit content.
    /// </summary>
    public interface IVideoComposer
    {
        /// <summary>
        /// Composes videos for a batch of top Reddit posts.
        /// </summary>
        /// <param name="progress">An object to report progress on the overall operation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task ComposeVideoAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken);

        /// <summary>
        /// Composes a single video from a specific Reddit post's title and comments.
        /// </summary>
        /// <param name="title">The title of the Reddit post.</param>
        /// <param name="comments">A list of comments from the post.</param>
        /// <param name="progress">An object to report progress on the video generation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <param name="outputPath">The path to save the final rendered video.</param>
        Task ComposeVideoAsync(string title, List<string> comments, IProgress<ProgressReport> progress, CancellationToken cancellationToken, string outputPath);
    }
}
