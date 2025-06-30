using RedditVideoStudio.Domain.Models;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    public interface IVideoDestination
    {
        string Name { get; }
        bool IsAuthenticated { get; }
        Task AuthenticateAsync(CancellationToken cancellationToken = default);
        Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, CancellationToken cancellationToken = default);
        Task SignOutAsync();
    }
}