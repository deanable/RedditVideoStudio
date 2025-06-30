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
    }
}