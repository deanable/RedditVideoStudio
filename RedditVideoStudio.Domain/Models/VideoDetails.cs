// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Domain\Models\VideoDetails.cs

using System;

namespace RedditVideoStudio.Domain.Models
{
    /// <summary>
    /// A simple data class to hold all the details for a video upload.
    /// </summary>
    public class VideoDetails
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public bool IsPrivate { get; set; } = true;

        // --- FIX: Added property to hold the scheduled publish time ---
        /// <summary>
        /// The UTC date and time when the video should be published.
        /// This is null if the video is not scheduled.
        /// </summary>
        public DateTime? ScheduledPublishTime { get; set; }
    }
}