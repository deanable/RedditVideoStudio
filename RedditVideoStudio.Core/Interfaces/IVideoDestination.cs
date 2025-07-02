namespace RedditVideoStudio.Core.Interfaces
{
    using RedditVideoStudio.Domain.Models;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IVideoDestination
    {
        string Name { get; }
        bool IsAuthenticated { get; }
        Task AuthenticateAsync(CancellationToken cancellationToken = default);
        Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, string? thumbnailPath, CancellationToken cancellationToken = default);
        Task SignOutAsync();
    }
}
