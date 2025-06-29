using System;

namespace RedditVideoStudio.Domain.Models
{
    /// <summary>
    /// Represents a request to upload a video to a service like YouTube.
    /// It contains the path to the video file and all the necessary metadata
    /// such as title, description, and the scheduled publish time.
    /// </summary>
    public class UploadRequest
    {
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime PublishTimeUtc { get; set; }
    }
}