using Microsoft.Win32;
using RedditVideoStudio.Core.Interfaces; // Using Core for interfaces, as you correctly pointed out
using RedditVideoStudio.UI.ViewModels;
using System;
using System.Windows;

namespace RedditVideoStudio.UI
{
    /// <summary>
    /// The code-behind for the SettingsWindow.
    /// Its responsibility is to interact with services to load and save settings.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;
        private readonly ISettingsService _settingsService;
        private readonly IFfmpegService _ffmpegService;

        /// <summary>
        /// The constructor receives all necessary services via dependency injection.
        /// The DI container in App.xaml.cs knows how to create and provide these.
        /// </summary>
        public SettingsWindow(SettingsViewModel viewModel, ISettingsService settingsService, IFfmpegService ffmpegService)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _settingsService = settingsService;
            _ffmpegService = ffmpegService;

            // Load a fresh, editable copy of the settings into the ViewModel.
            _viewModel.Settings = _settingsService.GetSettings();

            // Set the DataContext for the entire window to our ViewModel.
            // This allows the XAML to bind to properties like 'Settings' and 'Destinations'.
            DataContext = _viewModel;
        }

        /// <summary>
        /// When the user clicks "Save and Close", we use the SettingsService
        /// to persist the changes from the ViewModel.
        /// </summary>
        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.SaveSettings(_viewModel.Settings);
            DialogResult = true;
            Close();
        }

        // The methods below handle opening a file dialog for the user to select video clips.
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
        /// Helper method to show an OpenFileDialog for video files.
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
    }
}