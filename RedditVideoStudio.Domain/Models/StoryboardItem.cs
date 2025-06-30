using System;

namespace RedditVideoStudio.Domain.Models
{
    /// <summary>
    /// Represents a single piece of content on the storyboard timeline.
    /// This includes the paths to the generated image and audio files,
    /// as well as the precise start and end times for its appearance in the video.
    /// </summary>
    public class StoryboardItem
    {
        public string ImagePath { get; set; } = string.Empty;
        public string AudioPath { get; set; } = string.Empty;

        // This is the corrected, simple property. It only stores the value it is given.
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public string Position { get; set; } = "center";
    }
}