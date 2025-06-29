namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains settings specific to the Azure Text-to-Speech service,
    /// including the necessary API key and region for authentication.
    /// </summary>
    public class AzureTtsSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
    }
}
