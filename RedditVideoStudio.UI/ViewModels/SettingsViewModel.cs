using RedditVideoStudio.Infrastructure.Services; // Required for WindowsTextToSpeechService
using RedditVideoStudio.Shared.Configuration;
using System.Collections.ObjectModel; // Required for ObservableCollection
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RedditVideoStudio.UI.ViewModels
{
    /// <summary>
    /// The ViewModel for the Settings window. It holds a copy of the application's
    /// settings, allowing users to edit them in the UI. Changes are only applied
    /// back to the main configuration when the user saves them.
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private AppSettings _settings;
        public AppSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// A collection of available Windows TTS voice names to be displayed in the UI.
        /// This is populated by the WindowsTextToSpeechService.
        /// </summary>
        public ObservableCollection<string> Voices { get; set; }

        /// <summary>
        /// Initializes a new instance of the SettingsViewModel.
        /// </summary>
        /// <param name="windowsTtsService">The Windows TTS service, injected by the DI container.</param>
        public SettingsViewModel(WindowsTextToSpeechService windowsTtsService)
        {
            // Initialize with default settings to avoid null reference issues.
            _settings = new AppSettings();

            // Get the list of installed voices from the service and populate the collection
            // that the settings window's ComboBox will bind to.
            Voices = new ObservableCollection<string>(windowsTtsService.GetVoices());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}