namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains settings specific to the TikTok Content Posting API.
    /// </summary>
    public class TikTokSettings
    {
        /// <summary>
        /// The Client Key for your TikTok application.
        /// </summary>
        public string? ClientKey { get; set; }

        /// <summary>
        /// The Client Secret for your TikTok application.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// The scopes required for the application's API access.
        /// For sandbox testing, the scope must end with '.sandbox'.
        /// </summary>
        public string Scopes { get; set; } = "user.info.basic.sandbox"; // Temporarily change to a basic, auto-approved scope

        /// <summary>
        /// The callback URL where the user will be redirected after authorization.
        /// This must exactly match one of the URIs registered in the TikTok developer portal.
        /// For desktop apps, a localhost address is typically used.
        /// </summary>
        public string RedirectUri { get; set; } = "http://localhost:8912/callback/";
    }
}