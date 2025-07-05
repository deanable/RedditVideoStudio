// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Core\Interfaces\IVideoDestination.cs

namespace RedditVideoStudio.Core.Interfaces
{
    using RedditVideoStudio.Domain.Models;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IVideoDestination
    {
        string Name { get; }
        bool IsAuthenticated { get; }
        Task AuthenticateAsync(CancellationToken cancellationToken = default);
        Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, string? thumbnailPath, CancellationToken cancellationToken = default);
        Task SignOutAsync();
        Task<bool> DoesVideoExistAsync(string title, CancellationToken cancellationToken = default);
        Task<HashSet<string>> GetUploadedVideoTitlesAsync(CancellationToken cancellationToken = default);
    }
}