using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that uploads videos to YouTube. [cite: 1]
    /// </summary>
    public interface IYouTubeUploadService
    {
        /// <summary>
        /// Asynchronously uploads a video and its thumbnail to YouTube. [cite: 1]
        /// </summary>
        Task<string?> UploadVideoAsync(
             string videoPath,
             string thumbnailPath,
             string title,
             string description,
             DateTime? scheduledPublishTimeUtc,
             CancellationToken cancellationToken = default);

        // ADDED: New method contract for fetching video titles.
        Task<HashSet<string>> FetchUploadedVideoTitlesAsync(CancellationToken cancellationToken);
    }
}