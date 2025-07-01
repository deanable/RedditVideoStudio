using Microsoft.Win32;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Infrastructure.Services;
using RedditVideoStudio.UI.ViewModels;
using System;
using System.Linq;
using System.Windows;

namespace RedditVideoStudio.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;
        private readonly ISettingsService _settingsService;
        private readonly IFfmpegService _ffmpegService;
        private readonly WindowsTextToSpeechService _windowsTtsService;

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

            _viewModel.Settings = _settingsService.GetSettings();
            _viewModel.Voices = _windowsTtsService.GetVoices().ToList();

            DataContext = _viewModel;
        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            // First, update the settings dictionary from the viewmodel states
            foreach (var destVm in _viewModel.Destinations.Destinations)
            {
                _viewModel.Settings.EnabledDestinations[destVm.Name] = destVm.IsEnabled;
            }

            // Now, save the updated settings object
            _settingsService.SaveSettings(_viewModel.Settings);
            DialogResult = true;
            Close();
        }

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