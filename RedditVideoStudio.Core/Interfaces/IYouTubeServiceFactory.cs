using Google.Apis.Auth.OAuth2;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines a factory for creating an instance of the YouTube Upload Service.
    /// This is necessary to handle the UserCredential which is obtained interactively
    /// in the UI layer and passed in at runtime.
    /// </summary>
    public interface IYouTubeServiceFactory
    {
        /// <summary>
        /// Creates an instance of the YouTube upload service.
        /// </summary>
        /// <param name="credential">The user's OAuth2 credential.</param>
        /// <returns>An instance of IYouTubeUploadService.</returns>
        IYouTubeUploadService Create(UserCredential credential);
    }
}
