using Google.Apis.Auth.OAuth2;
using RedditVideoStudio.Shared.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a unified application configuration service.
    /// This provides a single point of access for all settings, making the
    /// application easier to manage and test.
    /// </summary>
    public interface IAppConfiguration
    {
        /// <summary>
        /// Gets the current application settings.
        /// </summary>
        AppSettings Settings { get; }

        /// <summary>
        /// Asynchronously retrieves the YouTube client secrets needed for OAuth2 authentication.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The client secrets for the YouTube API.</returns>
        Task<ClientSecrets> GetYouTubeSecretsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously saves the current settings to the persistent store (e.g., the registry).
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task SaveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reloads the settings from the persistent store.
        /// </summary>
        void Reload();
    }
}
