using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Infrastructure.Services;
using RedditVideoStudio.Shared.Models;
using RedditVideoStudio.UI.Logging;
using RedditVideoStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RedditVideoStudio.UI
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IRedditService _redditService;
        private readonly IAppConfiguration _configService;
        private readonly IVideoComposer _videoComposer;

        private readonly ObservableCollection<RedditPostViewModel> _fetchedPosts = new();
        private CancellationTokenSource _cancellationTokenSource = new();

        public MainWindow(
             ILogger<MainWindow> logger,
             IServiceProvider serviceProvider,
             IRedditService redditService,
             IAppConfiguration configService,
             IVideoComposer videoComposer)
        {
            InitializeComponent();

            _logger = logger;
            _serviceProvider = serviceProvider;
            _redditService = redditService;
            _configService = configService;
            _videoComposer = videoComposer;

            RedditPostListBox.ItemsSource = _fetchedPosts;
            var textBoxSink = new TextBoxSink(LogTextBox, Dispatcher);
            DelegatingSink.SetSink(textBoxSink);

            _logger.LogInformation("Application Main Window Initialized.");
            _ = LoadTopPostsAsync();
        }

        private async void GenerateVideo_Click(object sender, RoutedEventArgs e)
        {
            var selectedPosts = RedditPostListBox.SelectedItems.Cast<RedditPostViewModel>().ToList();
            if (!selectedPosts.Any())
            {
                MessageBox.Show("Please select at least one Reddit post to generate a video.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var enabledDestinations = _configService.Settings.EnabledDestinations
                .Where(kvp => kvp.Value)
                .Select(kvp => _serviceProvider.GetServices<IVideoDestination>().First(s => s.Name == kvp.Key))
                .ToList();

            if (!enabledDestinations.Any())
            {
                MessageBox.Show("No destination platforms are enabled. Please enable and connect at least one in Settings.", "No Destinations", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GenerationProgressBar.Value = 0;
            _cancellationTokenSource = new CancellationTokenSource();

            IProgress<ProgressReport> progress = new Progress<ProgressReport>(report =>
            {
                GenerationProgressBar.Value = report.Percentage;
                _logger.LogInformation("[{Percentage}%] {Message}", report.Percentage, report.Message);
            });

            try
            {
                foreach (var destination in enabledDestinations)
                {
                    if (!destination.IsAuthenticated)
                    {
                        await destination.AuthenticateAsync(_cancellationTokenSource.Token);
                    }
                }

                foreach (var post in selectedPosts)
                {
                    foreach (var destination in enabledDestinations)
                    {
                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        var orientation = destination.Name == "TikTok" ? "Portrait" : "Landscape";
                        _logger.LogInformation("Processing post '{Title}' for destination '{Destination}' with orientation '{Orientation}'", post.Title, destination.Name, orientation);

                        string outputDir = Path.Combine(AppContext.BaseDirectory, "output");
                        Directory.CreateDirectory(outputDir);
                        string baseFilename = Shared.Utilities.FileUtils.SanitizeFileName(post.Title ?? string.Empty).Take(40).Aggregate("", (s, c) => s + c);
                        string finalVideoPath = Path.Combine(outputDir, $"{baseFilename}_{destination.Name}.mp4");

                        await _videoComposer.ComposeVideoAsync(post.Title ?? "", post.Comments, progress, _cancellationTokenSource.Token, finalVideoPath, orientation);

                        await destination.UploadVideoAsync(finalVideoPath, new VideoDetails { Title = post.Title ?? "" }, _cancellationTokenSource.Token);
                    }
                }
                MessageBox.Show("All tasks completed!", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private async Task LoadTopPostsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching top Reddit posts...");
                var posts = await _redditService.FetchFullPostDataAsync(CancellationToken.None);
                await Dispatcher.InvokeAsync(() =>
                {
                    _fetchedPosts.Clear();
                    foreach (var post in posts)
                    {
                        _fetchedPosts.Add(new RedditPostViewModel
                        {
                            Id = post.Id,
                            Title = post.Title,
                            Score = post.Score,
                            Subreddit = post.Subreddit,
                            Url = post.Url,
                            Permalink = post.Permalink,
                            Comments = post.Comments.ToList()
                        });
                    }
                });
                _logger.LogInformation("Loaded {Count} posts.", _fetchedPosts.Count);
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void ResetYouTubeAuth_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tokenFolderPath = Path.Combine(AppContext.BaseDirectory, "YouTube.Auth.Store");
                if (Directory.Exists(tokenFolderPath))
                {
                    Directory.Delete(tokenFolderPath, true);
                }
                _logger.LogInformation("YouTube authentication tokens have been deleted.");
                MessageBox.Show("YouTube authentication has been reset. You will be prompted to log in on the next upload.", "Auth Reset", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset YouTube authentication.");
                MessageBox.Show($"Could not reset authentication: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshPosts_Click(object sender, RoutedEventArgs e)
        {
            await LoadTopPostsAsync();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private void HandleException(Exception ex)
        {
            _logger.LogError(ex, "An error occurred: {ErrorMessage}", ex.Message);
            string title = "Error";
            string message = $"An unexpected error occurred:\n\n{ex.Message}";
            switch (ex)
            {
                case AppConfigurationException configEx:
                    title = "Configuration Error";
                    message = $"A configuration error occurred: {configEx.Message}\nPlease check your settings.";
                    break;
                case FfmpegException ffmpegEx:
                    title = "FFmpeg Error";
                    message = $"An error occurred while running FFmpeg: {ffmpegEx.Message}\n\nFFmpeg Output:\n{ffmpegEx.FfmpegErrorOutput}";
                    break;
                case ApiException:
                    title = "API Error";
                    message = $"An API error occurred: {ex.Message}\nPlease check your internet connection and API keys.";
                    break;
                case TtsException:
                    title = "Text-to-Speech Error";
                    message = $"A text-to-speech error occurred: {ex.Message}\nPlease check your TTS settings and credentials.";
                    break;
                case OperationCanceledException:
                    title = "Operation Canceled";
                    message = "The operation was canceled.";
                    _logger.LogWarning("The operation was canceled by the user.");
                    break;
            }
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}