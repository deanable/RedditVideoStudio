// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Domain\Models\RedditPostData.cs

namespace RedditVideoStudio.Domain.Models
{
    /// <summary>
    /// Represents the core data of a Reddit post, including its title,
    /// subreddit, and a list of comments. This is a fundamental model
    /// used throughout the application.
    /// </summary>
    public class RedditPostData
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        // --- ADDED: Property to hold the post's body ---
        public string SelfText { get; set; } = "";
        public string Subreddit { get; set; } = "";
        public string Url { get; set; } = "";
        public string Permalink { get; set; } = "";
        public int Score { get; set; }
        public List<string> Comments { get; set; } = new();

        /// <summary>
        /// The UTC date and time when the video should be published.
        /// This is null if the video is not scheduled.
        /// </summary>
        public DateTime? ScheduledPublishTimeUtc { get; set; }
    }
}