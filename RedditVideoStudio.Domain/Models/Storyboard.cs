using System;
using System.Collections.Generic;
using System.Linq;

namespace RedditVideoStudio.Domain.Models
{
    /// <summary>
    /// Represents the timeline for a video segment. It contains a list of
    /// StoryboardItem objects, each representing a piece of content (like an
    /// image overlay with corresponding audio) and its timing.
    /// </summary>
    public class Storyboard
    {
        public List<StoryboardItem> Items { get; set; } = new();

        /// <summary>
        /// Calculates the start time for the next item to be added to the storyboard.
        /// This is determined by finding the maximum end time of all existing items.
        /// </summary>
        /// <returns>The TimeSpan for the next item's start time.</returns>
        public TimeSpan GetNextStartTime()
        {
            return Items.Any() ? Items.Max(i => i.EndTime) : TimeSpan.Zero;
        }
    }
}
