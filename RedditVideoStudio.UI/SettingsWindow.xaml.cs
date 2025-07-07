using Microsoft.Win32;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Infrastructure.Services;
using RedditVideoStudio.Shared.Configuration;
using RedditVideoStudio.UI.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace RedditVideoStudio.UI
{
    /// <summary>
    /// Interaction logic for the settings window.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // The main view model for the settings window.
        private readonly SettingsViewModel _viewModel;
        // Service for loading and saving application settings.
        private readonly ISettingsService _settingsService;
        // Service for FFmpeg operations, like getting video duration.
        private readonly IFfmpegService _ffmpegService;
        // Service for getting available Windows TTS voices.
        private readonly WindowsTextToSpeechService _windowsTtsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
        /// </summary>
        public SettingsWindow(
            SettingsViewModel viewModel,
            ISettingsService settingsService,
            IFfmpegService ffmpegService,
            WindowsTextToSpeechService windowsTtsService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _settingsService = settingsService;
            _ffmpegService = ffmpegService;
            _windowsTtsService = windowsTtsService;

            // Load existing settings into the ViewModel.
            _viewModel.Settings = _settingsService.GetSettings();

            // ==============================================================================
            // FIX: This new block synchronizes the loaded settings with the UI.
            // It iterates through each destination view model and sets its IsEnabled property
            // based on the value stored in the settings dictionary.
            // ==============================================================================
            foreach (var destVm in _viewModel.Destinations.Destinations)
            {
                // Look up the saved setting for the current destination.
                // If a value isn't found in the dictionary, it defaults to false.
                destVm.IsEnabled = _viewModel.Settings.EnabledDestinations.GetValueOrDefault(destVm.Name, false);
            }

            // Populate the list of available Windows TTS voices.
            _viewModel.Voices = _windowsTtsService.GetVoices().ToList();

            // Set the DataContext for the window to the main view model.
            DataContext = _viewModel;
        }

        /// <summary>
        /// Handles the click event for the "Save and Close" button.
        /// </summary>
        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            // First, update the settings dictionary for enabled destinations from the viewmodel states.
            foreach (var destVm in _viewModel.Destinations.Destinations)
            {
                _viewModel.Settings.EnabledDestinations[destVm.Name] = destVm.IsEnabled;
            }

            // Now, save the updated settings object.
            _settingsService.SaveSettings(_viewModel.Settings);
            DialogResult = true;
            Close();
        }

        // Event handlers for video file selection dialogs
        #region Video File Dialogs

        private async void IntroHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var path = ShowVideoFileDialog("Select Intro Video");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var duration = await _ffmpegService.GetVideoDurationAsync(path);
                    IntroDurationSlider.Maximum = duration.TotalSeconds;
                    _viewModel.Settings.ClipSettings.IntroPath = path;
                    _viewModel.Settings.ClipSettings.IntroDuration = duration.TotalSeconds / 2;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not read video duration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BreakHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var path = ShowVideoFileDialog("Select Break Video");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var duration = await _ffmpegService.GetVideoDurationAsync(path);
                    BreakDurationSlider.Maximum = duration.TotalSeconds;
                    _viewModel.Settings.ClipSettings.BreakClipPath = path;
                    _viewModel.Settings.ClipSettings.BreakClipDuration = duration.TotalSeconds / 2;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not read video duration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OutroHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var path = ShowVideoFileDialog("Select Outro Video");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var duration = await _ffmpegService.GetVideoDurationAsync(path);
                    OutroDurationSlider.Maximum = duration.TotalSeconds;
                    _viewModel.Settings.ClipSettings.OutroPath = path;
                    _viewModel.Settings.ClipSettings.OutroDuration = duration.TotalSeconds / 2;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not read video duration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Shows a file dialog for selecting video files.
        /// </summary>
        private string ShowVideoFileDialog(string title)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.mov)|*.mp4;*.mov|All files (*.*)|*.*",
                Title = title
            };
            return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : string.Empty;
        }

        #endregion

        /// <summary>
        /// Handles the click event for the "Load from client_secrets.json" button.
        /// </summary>
        private void LoadYouTubeSecrets_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select client_secrets.json",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var jsonContent = File.ReadAllText(openFileDialog.FileName);
                    // Parse the JSON to find the 'installed' object which contains credentials for desktop apps.
                    using var jsonDoc = JsonDocument.Parse(jsonContent);
                    if (jsonDoc.RootElement.TryGetProperty("installed", out var installedElement) &&
                        installedElement.TryGetProperty("client_id", out var clientIdElement) &&
                        installedElement.TryGetProperty("client_secret", out var clientSecretElement))
                    {
                        var clientId = clientIdElement.GetString();
                        var clientSecret = clientSecretElement.GetString();
                        if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
                        {
                            // To ensure the UI updates, we replace the Settings object with a new instance
                            // containing the updated credentials. This triggers the OnPropertyChanged event.
                            var updatedSettings = _settingsService.GetSettings(); // Get a fresh copy
                            updatedSettings.YouTube.ClientId = clientId;
                            updatedSettings.YouTube.ClientSecret = clientSecret;

                            // Re-assigning the property on the ViewModel will notify the UI to refresh.
                            _viewModel.Settings = updatedSettings;
                            MessageBox.Show("YouTube API credentials loaded successfully from file.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            throw new JsonException("The JSON file is missing the 'client_id' or 'client_secret'.");
                        }
                    }
                    else
                    {
                        throw new JsonException("The selected JSON file is not a valid Google Cloud client_secrets file for a Desktop App. It must contain an 'installed' property.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load or parse the JSON file.\n\nError: {ex.Message}", "File Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}