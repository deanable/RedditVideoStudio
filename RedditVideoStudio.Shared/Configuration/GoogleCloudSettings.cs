namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains settings related to Google Cloud Platform services,
    /// primarily the path to the service account key for authentication.
    /// </summary>
    public class GoogleCloudSettings
    {
        public string ServiceAccountKeyPath { get; set; } = string.Empty;
    }
}
