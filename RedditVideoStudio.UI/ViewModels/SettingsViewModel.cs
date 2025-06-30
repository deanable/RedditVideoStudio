using CommunityToolkit.Mvvm.ComponentModel;
using RedditVideoStudio.Shared.Configuration;
using RedditVideoStudio.UI.ViewModels.Settings;
using System.Collections.Generic;

namespace RedditVideoStudio.UI.ViewModels
{
    /// <summary>
    /// The ViewModel for the main SettingsWindow.
    /// It holds the settings currently being edited and contains sub-ViewModels for different settings sections.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private AppSettings _settings;

        /// <summary>
        /// A sub-ViewModel that manages the collection of destination platforms (like YouTube, TikTok, etc.).
        /// </summary>
        public DestinationsSettingsViewModel Destinations { get; }

        /// <summary>
        /// A list of available voice names, specifically for the Windows TTS provider.
        /// This is populated by the SettingsWindow's code-behind.
        /// </summary>
        public List<string> Voices { get; set; } = new List<string>();

        public SettingsViewModel(DestinationsSettingsViewModel destinationsViewModel)
        {
            // The source generator will create the public 'Settings' property from the private '_settings' field.
            _settings = new AppSettings();
            // The DestinationsSettingsViewModel is injected by the DI container and assigned here.
            Destinations = destinationsViewModel;
        }
    }
}