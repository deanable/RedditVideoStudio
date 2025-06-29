namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Holds configuration settings for the Pexels API, which is used
    /// to source background videos and images.
    /// </summary>
    public class PexelsSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string DefaultQuery { get; set; } = "nature";
    }
}
