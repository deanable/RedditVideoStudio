// In: C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Core\Interfaces\IYouTubeUploadService.cs

using RedditVideoStudio.Shared.Models; // Add this using statement
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that uploads videos to YouTube.
    /// </summary>
    public interface IYouTubeUploadService
    {
        /// <summary>
        /// Asynchronously uploads a video and its thumbnail to YouTube.
        /// </summary>
        // --- START OF MODIFICATION ---
        Task<string?> UploadVideoAsync(
             string videoPath,
             string thumbnailPath,
             string title,
             string description,
             DateTime? scheduledPublishTimeUtc,
             IProgress<ProgressReport> progress, // Add IProgress parameter
             CancellationToken cancellationToken = default);
        // --- END OF MODIFICATION ---

        Task<List<string>> FetchUploadedVideoTitlesAsync(CancellationToken cancellationToken);
    }
}