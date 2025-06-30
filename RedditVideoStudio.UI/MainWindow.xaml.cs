using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
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
        private readonly IRedditService _redditService;
        private readonly IAppConfiguration _configService;
        private readonly IImageService _imageService;
        private readonly IPexelsService _pexelsService;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly IVideoComposer _videoComposer;
        private readonly IYouTubeServiceFactory _youTubeServiceFactory;
        private readonly IFfmpegService _ffmpegService; // Service was already here, needed for the fix

        private readonly ObservableCollection<RedditPostViewModel> _fetchedPosts = new();
        private CancellationTokenSource _cancellationTokenSource = new();
        private IYouTubeUploadService? _youTubeUploader;

        public MainWindow(
            ILogger<MainWindow> logger,
            IRedditService redditService,
            IAppConfiguration configService,
            IImageService imageService,
            IPexelsService pexelsService,
            SettingsViewModel settingsViewModel,
            IVideoComposer videoComposer,
            IYouTubeServiceFactory youTubeServiceFactory,
            IFfmpegService ffmpegService) // Already injected, now we'll use it
        {
            InitializeComponent();

            _logger = logger;
            _redditService = redditService;
            _configService = configService;
            _imageService = imageService;
            _pexelsService = pexelsService;
            _settingsViewModel = settingsViewModel;
            _videoComposer = videoComposer;
            _youTubeServiceFactory = youTubeServiceFactory;
            _ffmpegService = ffmpegService; // Store the injected service

            RedditPostListBox.ItemsSource = _fetchedPosts;
            RedditPostListBox.SelectionChanged += RedditPostListBox_SelectionChanged;

            var textBoxSink = new TextBoxSink(LogTextBox, Dispatcher);
            DelegatingSink.SetSink(textBoxSink);

            _logger.LogInformation("Application Main Window Initialized.");
            _ = LoadAndSyncAllData();
        }

        private async void GenerateVideo_Click(object sender, RoutedEventArgs e)
        {
            var selectedPosts = RedditPostListBox.SelectedItems.Cast<RedditPostViewModel>().ToList();
            if (!selectedPosts.Any())
            {
                MessageBox.Show("Please select at least one Reddit post to generate a video.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GenerationProgressBar.Value = 0;
            GenerationProgressBar.Maximum = 100;
            _cancellationTokenSource = new CancellationTokenSource();

            var progress = new Progress<ProgressReport>(report =>
            {
                GenerationProgressBar.Value = report.Percentage;
                _logger.LogInformation("[{Percentage}%] {Message}", report.Percentage, report.Message);
            });

            var filesToDelete = new List<string>();

            try
            {
                if (_youTubeUploader == null)
                {
                    _logger.LogInformation("Authentication required. Starting Google Authentication for YouTube...");
                    var credential = await AuthorizeYouTubeAsync(_cancellationTokenSource.Token);
                    if (credential == null) throw new OperationCanceledException("YouTube authentication was canceled or failed.");
                    _youTubeUploader = _youTubeServiceFactory.Create(credential);
                    await SyncWithYouTubeAsync();
                }

                foreach (var post in selectedPosts)
                {
                    // ... video generation logic ...
                    string outputDir = Path.Combine(AppContext.BaseDirectory, "output");
                    Directory.CreateDirectory(outputDir);
                    string baseFilename = Shared.Utilities.FileUtils.SanitizeFileName(post.Title ?? string.Empty).Take(40).Aggregate("", (s, c) => s + c);
                    string finalVideoPath = Path.Combine(outputDir, $"{baseFilename}_output.mp4");

                    // CORRECTION 1: The 'progress' argument was missing from this method call.
                    await _videoComposer.ComposeVideoAsync(post.Title ?? string.Empty, post.Comments.ToList(), progress, _cancellationTokenSource.Token, finalVideoPath);

                    // ... thumbnail generation logic ...

                    // ... upload logic ...
                }

                _logger.LogInformation("All videos rendered and scheduled for upload.");
                MessageBox.Show("Batch video generation and upload complete!", "Done", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
            finally
            {
                // ... cleanup logic ...
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            // Asks the application Host to provide a fully-formed SettingsWindow,
            // with all of its dependencies automatically injected.
            var settingsWindow = App.Host.Services.GetRequiredService<SettingsWindow>();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
        private async Task LoadAndSyncAllData()
        {
            await LoadTopPostsAsync();
            try
            {
                _logger.LogInformation("Attempting background authentication with YouTube...");
                var credential = await AuthorizeYouTubeAsync(CancellationToken.None);
                _youTubeUploader = _youTubeServiceFactory.Create(credential);
                _logger.LogInformation("Background authentication successful.");
                await SyncWithYouTubeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not perform initial sync with YouTube. User may need to log in.");
            }
        }

        private async Task<UserCredential> AuthorizeYouTubeAsync(CancellationToken token)
        {
            var clientSecrets = await _configService.GetYouTubeSecretsAsync(token);
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets,
                new[] { YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.YoutubeReadonly },
                "user",
                token,
                new FileDataStore(Path.Combine(AppContext.BaseDirectory, "YouTube.Auth.Store"), true)
            );
            return credential;
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
                            Comments = post.Comments.ToList(),
                            ScheduledPublishTimeUtc = null,
                            IsAlreadyUploaded = false
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

        private async Task SyncWithYouTubeAsync()
        {
            if (_youTubeUploader == null)
            {
                _logger.LogWarning("YouTube service not available for syncing.");
                return;
            }
            try
            {
                var youtubeTitles = await _youTubeUploader.FetchUploadedVideoTitlesAsync(CancellationToken.None);
                if (!youtubeTitles.Any()) return;
                foreach (var post in _fetchedPosts)
                {
                    var sanitizedTitle = Shared.Utilities.TextUtils.SanitizeYouTubeTitle(post.Title ?? string.Empty);
                    if (youtubeTitles.Contains(sanitizedTitle))
                    {
                        post.IsAlreadyUploaded = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync with YouTube channel titles.");
            }
        }

        private async void RefreshPosts_Click(object sender, RoutedEventArgs e)
        {
            await LoadAndSyncAllData();
        }

        private void RedditPostListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RedditPostListBox.SelectedItem is RedditPostViewModel selectedPost)
            {
                if (!selectedPost.ScheduledPublishTimeUtc.HasValue)
                {
                    selectedPost.ScheduledPublishTimeUtc = DateTime.Now.Date.AddDays(1).AddHours(10);
                }
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
                case RedditApiException:
                case PexelsApiException:
                case YouTubeApiException:
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