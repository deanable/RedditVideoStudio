using Microsoft.Win32;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Shared.Configuration;
using RedditVideoStudio.UI.ViewModels;
using System.Text.Json;
using System.Windows;

namespace RedditVideoStudio.UI
{
    /// <summary>
    /// Interaction logic for the SettingsWindow.xaml.
    /// This window provides a user interface for editing the application's settings.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;
        private readonly IAppConfiguration _configService;

        public SettingsWindow(SettingsViewModel viewModel, IAppConfiguration configService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _configService = configService;

            // Create a deep copy of the settings object for editing. This prevents changes
            // from being applied if the user cancels the dialog.
            var settingsAsJson = JsonSerializer.Serialize(_configService.Settings);
            _viewModel.Settings = JsonSerializer.Deserialize<AppSettings>(settingsAsJson) ?? new AppSettings();

            DataContext = _viewModel;
        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            // If a duration slider is set to 0, clear the associated clip path as this implies the clip is disabled.
            if (_viewModel.Settings.ClipSettings.IntroDuration == 0)
            {
                _viewModel.Settings.ClipSettings.IntroPath = string.Empty;
            }
            if (_viewModel.Settings.ClipSettings.BreakClipDuration == 0)
            {
                _viewModel.Settings.ClipSettings.BreakClipPath = string.Empty;
            }
            if (_viewModel.Settings.ClipSettings.OutroDuration == 0)
            {
                _viewModel.Settings.ClipSettings.OutroPath = string.Empty;
            }

            // Apply the edited settings back to the main configuration service instance.
            _configService.Settings.Reddit = _viewModel.Settings.Reddit;
            _configService.Settings.Pexels = _viewModel.Settings.Pexels;
            _configService.Settings.Tts = _viewModel.Settings.Tts;
            _configService.Settings.Ffmpeg = _viewModel.Settings.Ffmpeg;
            _configService.Settings.YouTube = _viewModel.Settings.YouTube;
            _configService.Settings.ImageGeneration = _viewModel.Settings.ImageGeneration;
            _configService.Settings.GoogleCloud = _viewModel.Settings.GoogleCloud;
            _configService.Settings.ClipSettings = _viewModel.Settings.ClipSettings;
            _configService.Settings.AzureTts = _viewModel.Settings.AzureTts;
            _configService.Settings.ElevenLabs = _viewModel.Settings.ElevenLabs;

            DialogResult = true;
            Close();
        }

        private void GoogleKeyHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select Google Service Account Key"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.Settings.GoogleCloud.ServiceAccountKeyPath = openFileDialog.FileName;
            }
        }

        private void IntroHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.mov)|*.mp4;*.mov|All files (*.*)|*.*",
                Title = "Select Intro Video"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.Settings.ClipSettings.IntroPath = openFileDialog.FileName;
            }
        }

        private void BreakHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.mov)|*.mp4;*.mov|All files (*.*)|*.*",
                Title = "Select Break Video"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.Settings.ClipSettings.BreakClipPath = openFileDialog.FileName;
            }
        }

        private void OutroHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.mov)|*.mp4;*.mov|All files (*.*)|*.*",
                Title = "Select Outro Video"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.Settings.ClipSettings.OutroPath = openFileDialog.FileName;
            }
        }
    }
}