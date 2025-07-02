namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains settings related to uploading videos to YouTube.
    /// </summary>
    public class YouTubeSettings
    {
        public string PrivacyStatus { get; set; } = "private"; // "private", "unlisted", or "public"
        public int AutoScheduleIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// The Client ID for your YouTube application from Google Cloud Console.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// The Client Secret for your YouTube application from Google Cloud Console.
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;
    }
}
