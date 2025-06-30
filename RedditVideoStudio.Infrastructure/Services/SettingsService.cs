using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Shared.Configuration;
using System.Text.Json;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly IAppConfiguration _appConfiguration;

        public SettingsService(IAppConfiguration appConfiguration)
        {
            _appConfiguration = appConfiguration;
        }

        public AppSettings GetSettings()
        {
            var settingsAsJson = JsonSerializer.Serialize(_appConfiguration.Settings);
            return JsonSerializer.Deserialize<AppSettings>(settingsAsJson) ?? new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            _appConfiguration.Settings.Reddit = settings.Reddit;
            _appConfiguration.Settings.Pexels = settings.Pexels;
            _appConfiguration.Settings.Tts = settings.Tts;
            _appConfiguration.Settings.Ffmpeg = settings.Ffmpeg;
            _appConfiguration.Settings.YouTube = settings.YouTube;
            _appConfiguration.Settings.TikTok = settings.TikTok;
            _appConfiguration.Settings.ImageGeneration = settings.ImageGeneration;
            _appConfiguration.Settings.GoogleCloud = settings.GoogleCloud;
            _appConfiguration.Settings.ClipSettings = settings.ClipSettings;
            _appConfiguration.Settings.AzureTts = settings.AzureTts;
            _appConfiguration.Settings.ElevenLabs = settings.ElevenLabs;

            _appConfiguration.Save();
        }
    }
}