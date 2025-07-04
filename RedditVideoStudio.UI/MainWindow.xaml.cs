// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.UI\MainWindow.xaml.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Shared.Models;
using RedditVideoStudio.Shared.Utilities; // Added for TextUtils
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
        private readonly IPublishingService _publishingService;
        private readonly ObservableCollection<RedditPostViewModel> _fetchedPosts = new();
        private CancellationTokenSource _cancellationTokenSource = new();

        public MainWindow(
             ILogger<MainWindow> logger,
             IServiceProvider serviceProvider,
             IRedditService redditService,
             IAppConfiguration configService,
             IPublishingService publishingService)
        {
            InitializeComponent();
            _logger = logger;
            _serviceProvider = serviceProvider;
            _redditService = redditService;
            _configService = configService;
            _publishingService = publishingService;

            RedditPostListBox.ItemsSource = _fetchedPosts;
            var textBoxSink = new TextBoxSink(LogTextBox, Dispatcher);
            DelegatingSink.SetSink(textBoxSink);

            _logger.LogInformation("Application Main Window Initialized.");
            // Fire and forget the loading process
            _ = LoadTopPostsAsync();
        }

        private async void GenerateVideo_Click(object sender, RoutedEventArgs e)
        {
            var selectedPosts = RedditPostListBox.SelectedItems.Cast<RedditPostViewModel>()
                                               .Where(p => !p.IsAlreadyUploaded) // Ensure we only process non-uploaded posts
                                               .ToList();
            if (!selectedPosts.Any())
            {
                MessageBox.Show("Please select at least one Reddit post that has not already been uploaded.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var enabledDestinationNames = _configService.Settings.EnabledDestinations
                .Where(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();

            if (!enabledDestinationNames.Any())
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
                foreach (var post in selectedPosts)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    var postData = new RedditPostData
                    {
                        Title = post.Title ?? string.Empty,
                        Comments = post.Comments
                    };
                    await _publishingService.PublishVideoAsync(postData, enabledDestinationNames, progress, _cancellationTokenSource.Token);
                    // Mark as uploaded after successful processing
                    post.IsAlreadyUploaded = true;
                }
                MessageBox.Show("All publishing tasks completed!", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
            finally
            {
                GenerationProgressBar.Value = 0;
            }
        }

        private async Task LoadTopPostsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching top Reddit posts...");
                var posts = await _redditService.FetchFullPostDataAsync(CancellationToken.None);

                // Get the YouTube destination service to check for existing videos
                var youTubeDestination = _serviceProvider.GetRequiredService<IEnumerable<IVideoDestination>>().FirstOrDefault(d => d.Name == "YouTube");
                HashSet<string> uploadedTitles = new();

                if (youTubeDestination != null && youTubeDestination.IsAuthenticated)
                {
                    uploadedTitles = await youTubeDestination.GetUploadedVideoTitlesAsync(CancellationToken.None);
                }
                else
                {
                    _logger.LogWarning("YouTube is not authenticated. Cannot check for existing videos.");
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _fetchedPosts.Clear();
                    foreach (var post in posts)
                    {
                        var sanitizedTitle = TextUtils.SanitizeYouTubeTitle(post.Title);
                        _fetchedPosts.Add(new RedditPostViewModel
                        {
                            Id = post.Id,
                            Title = post.Title,
                            Score = post.Score,
                            Subreddit = post.Subreddit,
                            Url = post.Url,
                            Permalink = post.Permalink,
                            Comments = post.Comments.ToList(),
                            // Set the property based on whether the title exists in the fetched list
                            IsAlreadyUploaded = uploadedTitles.Contains(sanitizedTitle)
                        });
                    }
                });

                _logger.LogInformation("Loaded {Count} posts. Marked {UploadedCount} as already uploaded to YouTube.", _fetchedPosts.Count, _fetchedPosts.Count(p => p.IsAlreadyUploaded));
            }
            catch (Exception ex)
            {
                HandleException(ex);
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
            // After settings close, reload posts to reflect any new authentications
            _ = LoadTopPostsAsync();
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