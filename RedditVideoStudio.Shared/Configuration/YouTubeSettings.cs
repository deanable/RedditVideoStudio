namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains settings related to uploading videos to YouTube,
    /// such as the default privacy status for new uploads.
    /// </summary>
    public class YouTubeSettings
    {
        public string PrivacyStatus { get; set; } = "private"; // "private", "unlisted", or "public"

        /// <summary>
        /// The interval in minutes to wait between automatically scheduled video uploads.
        /// </summary>
        public int AutoScheduleIntervalMinutes { get; set; } = 30;
    }
}