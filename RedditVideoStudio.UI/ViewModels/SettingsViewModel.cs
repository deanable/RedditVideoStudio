using CommunityToolkit.Mvvm.ComponentModel;
using RedditVideoStudio.Shared.Configuration;
using RedditVideoStudio.UI.ViewModels.Settings;
using System.Collections.Generic;

namespace RedditVideoStudio.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private AppSettings _settings;

        public DestinationsSettingsViewModel Destinations { get; }

        public List<string> Voices { get; set; } = new List<string>();

        public SettingsViewModel(DestinationsSettingsViewModel destinationsViewModel)
        {
            // The source generator will create the public 'Settings' property from the private '_settings' field.
            _settings = new AppSettings();
            Destinations = destinationsViewModel;
        }
    }
}