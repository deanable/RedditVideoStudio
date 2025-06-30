// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Core\Interfaces\ITikTokServiceFactory.cs
namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines a factory for creating an instance of the TikTok Upload Service.
    /// This is necessary to handle the access token which is obtained at runtime.
    /// </summary>
    public interface ITikTokServiceFactory
    {
        ITikTokUploadService Create(string accessToken);
    }
}