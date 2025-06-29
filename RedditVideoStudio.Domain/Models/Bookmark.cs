using System;

namespace RedditVideoStudio.Domain.Models
{
    /// <summary>
    /// Represents a single word and its timing information within a synthesized audio track.
    /// This is used to align visual elements (like text overlays) with the spoken narration.
    /// </summary>
    public class Bookmark
    {
        /// <summary>
        /// The specific word that was spoken.
        /// </summary>
        public string Word { get; }

        /// <summary>
        /// The exact time at which the word begins in the audio track.
        /// </summary>
        public TimeSpan StartTime { get; }

        /// <summary>
        /// The duration for which the word is spoken.
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Initializes a new instance of the Bookmark class.
        /// </summary>
        /// <param name="word">The spoken word.</param>
        /// <param name="startTime">The start time of the word.</param>
        /// <param name="duration">The duration of the word.</param>
        public Bookmark(string word, TimeSpan startTime, TimeSpan duration)
        {
            Word = word;
            StartTime = startTime;
            Duration = duration;
        }
    }
}