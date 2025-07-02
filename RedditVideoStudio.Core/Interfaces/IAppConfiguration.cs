namespace RedditVideoStudio.Core.Interfaces
{
    using RedditVideoStudio.Shared.Configuration;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAppConfiguration
    {
        AppSettings Settings { get; }
        void Save();
        Task SaveAsync(CancellationToken cancellationToken = default);
        void Reload();
    }
}
