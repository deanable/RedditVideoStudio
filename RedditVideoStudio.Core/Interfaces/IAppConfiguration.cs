using Google.Apis.Auth.OAuth2;
using RedditVideoStudio.Shared.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    public interface IAppConfiguration
    {
        AppSettings Settings { get; }
        void Save();
        Task<ClientSecrets> GetYouTubeSecretsAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(CancellationToken cancellationToken = default);
        void Reload();
    }
}