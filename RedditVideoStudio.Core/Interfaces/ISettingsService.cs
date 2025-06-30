using RedditVideoStudio.Shared.Configuration;

namespace RedditVideoStudio.Core.Interfaces
{
    public interface ISettingsService
    {
        AppSettings GetSettings();
        void SaveSettings(AppSettings settings);
    }
}