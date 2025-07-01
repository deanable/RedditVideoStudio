using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Google.Apis.Auth.OAuth2;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Shared.Configuration;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class RegistryAppConfiguration : IAppConfiguration
    {
        private const string BaseRegistryKey = @"Software\RedditVideoStudio";
        private readonly ILogger<RegistryAppConfiguration> _logger;
        private AppSettings _settings;

        public AppSettings Settings => _settings;

        public RegistryAppConfiguration(ILogger<RegistryAppConfiguration> logger)
        {
            _logger = logger;
            _settings = LoadSettings();
        }

        public void Reload()
        {
            _settings = LoadSettings();
            _logger.LogInformation("Configuration reloaded from registry.");
        }

        public void Save()
        {
            try
            {
                using var baseKey = Registry.CurrentUser.CreateSubKey(BaseRegistryKey + @"\AppSettings");
                if (baseKey == null)
                {
                    throw new AppConfigurationException("Failed to create or open registry key for AppSettings.");
                }

                SaveSection(baseKey, nameof(AppSettings.Reddit), _settings.Reddit);
                SaveSection(baseKey, nameof(AppSettings.Pexels), _settings.Pexels);
                SaveSection(baseKey, nameof(AppSettings.Tts), _settings.Tts);
                SaveSection(baseKey, nameof(AppSettings.ImageGeneration), _settings.ImageGeneration);
                SaveSection(baseKey, nameof(AppSettings.YouTube), _settings.YouTube);
                SaveSection(baseKey, nameof(AppSettings.ClipSettings), _settings.ClipSettings);
                SaveSection(baseKey, nameof(AppSettings.GoogleCloud), _settings.GoogleCloud);
                SaveSection(baseKey, nameof(AppSettings.AzureTts), _settings.AzureTts);
                SaveSection(baseKey, nameof(AppSettings.Ffmpeg), _settings.Ffmpeg);
                SaveSection(baseKey, nameof(AppSettings.ElevenLabs), _settings.ElevenLabs);
                SaveSection(baseKey, nameof(AppSettings.TikTok), _settings.TikTok);

                _logger.LogInformation("Configuration saved successfully to the registry.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration to the registry.");
                throw new AppConfigurationException("Failed to save configuration.", ex);
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(() => Save(), cancellationToken);
        }

        public Task<ClientSecrets> GetYouTubeSecretsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var youtubeKey = Registry.CurrentUser.OpenSubKey($@"{BaseRegistryKey}\YouTubeSecrets");
                if (youtubeKey == null)
                {
                    throw new AppConfigurationException("YouTube secrets not found in the registry. Please ensure the .reg file has been imported.");
                }

                var secrets = new ClientSecrets
                {
                    ClientId = youtubeKey.GetValue("client_id") as string ?? string.Empty,
                    ClientSecret = youtubeKey.GetValue("client_secret") as string ?? string.Empty,
                };

                if (string.IsNullOrEmpty(secrets.ClientId) || string.IsNullOrEmpty(secrets.ClientSecret))
                {
                    throw new AppConfigurationException("YouTube client_id or client_secret is missing from the registry.");
                }

                return Task.FromResult(secrets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve YouTube secrets from the registry.");
                throw new AppConfigurationException("Failed to retrieve YouTube secrets.", ex);
            }
        }

        private AppSettings LoadSettings()
        {
            var settings = new AppSettings();
            const string appSettingsRegistryKey = BaseRegistryKey + @"\AppSettings";

            try
            {
                using var settingsKey = Registry.CurrentUser.OpenSubKey(appSettingsRegistryKey);
                if (settingsKey == null)
                {
                    _logger.LogWarning("AppSettings registry key not found. Loading default settings. Key will be created on save.");
                    return settings;
                }

                LoadSection(settingsKey, nameof(AppSettings.Reddit), settings.Reddit);
                LoadSection(settingsKey, nameof(AppSettings.Pexels), settings.Pexels);
                LoadSection(settingsKey, nameof(AppSettings.Tts), settings.Tts);
                LoadSection(settingsKey, nameof(AppSettings.ImageGeneration), settings.ImageGeneration);
                LoadSection(settingsKey, nameof(AppSettings.YouTube), settings.YouTube);
                LoadSection(settingsKey, nameof(AppSettings.ClipSettings), settings.ClipSettings);
                LoadSection(settingsKey, nameof(AppSettings.GoogleCloud), settings.GoogleCloud);
                LoadSection(settingsKey, nameof(AppSettings.AzureTts), settings.AzureTts);
                LoadSection(settingsKey, nameof(AppSettings.Ffmpeg), settings.Ffmpeg);
                LoadSection(settingsKey, nameof(AppSettings.ElevenLabs), settings.ElevenLabs);
                LoadSection(settingsKey, nameof(AppSettings.TikTok), settings.TikTok);

                _logger.LogInformation("Configuration loaded successfully from the registry.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading settings from the registry. Default settings will be used.");
                return new AppSettings();
            }

            return settings;
        }

        private void LoadSection(RegistryKey baseKey, string sectionName, object sectionObject)
        {
            using var sectionKey = baseKey.OpenSubKey(sectionName);
            if (sectionKey == null)
            {
                _logger.LogDebug("Registry section '{SectionName}' not found. Skipping.", sectionName);
                return;
            }

            foreach (var prop in sectionObject.GetType().GetProperties())
            {
                if (!prop.CanWrite) continue;

                var value = sectionKey.GetValue(prop.Name);
                if (value != null)
                {
                    try
                    {
                        var convertedValue = Convert.ChangeType(value, prop.PropertyType, CultureInfo.InvariantCulture);
                        prop.SetValue(sectionObject, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error loading registry value '{PropertyName}' in section '{SectionName}'.", prop.Name, sectionName);
                    }
                }
            }
        }

        private void SaveSection(RegistryKey baseKey, string sectionName, object sectionObject)
        {
            using var sectionKey = baseKey.CreateSubKey(sectionName);
            foreach (var prop in sectionObject.GetType().GetProperties())
            {
                if (!prop.CanRead) continue;

                var value = prop.GetValue(sectionObject);
                if (value != null)
                {
                    sectionKey.SetValue(prop.Name, value.ToString(), RegistryValueKind.String);
                }
            }
        }
    }
}