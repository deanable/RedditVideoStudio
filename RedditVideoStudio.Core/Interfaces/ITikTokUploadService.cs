// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Core\Interfaces\ITikTokUploadService.cs
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that uploads a video to TikTok.
    /// </summary>
    public interface ITikTokUploadService
    {
        Task UploadVideoAsync(string videoPath, string title, CancellationToken cancellationToken = default);
    }
}