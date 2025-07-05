// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.UI\MainWindow.xaml.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Shared.Models;
using RedditVideoStudio.Shared.Utilities;
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
            // Fire and forget the loading process with the new inverted logic
            _ = LoadTopPostsAsync();
        }

        private async void GenerateVideo_Click(object sender, RoutedEventArgs e)
        {
            var selectedPosts = RedditPostListBox.SelectedItems.Cast<RedditPostViewModel>()
                                               .Where(p => !p.IsAlreadyUploaded)
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
                MessageBox.Show("No destination platforms are enabled. Please enable at least one in Settings.", "No Destinations", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                foreach (var postViewModel in selectedPosts)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    var postData = new RedditPostData
                    {
                        Title = postViewModel.Title ?? string.Empty,
                        Comments = postViewModel.Comments,
                        Permalink = postViewModel.Permalink ?? string.Empty,
                        Subreddit = postViewModel.Subreddit ?? string.Empty,
                        ScheduledPublishTimeUtc = postViewModel.ScheduledPublishTimeUtc
                    };

                    await _publishingService.PublishVideoAsync(postData, enabledDestinationNames, progress, _cancellationTokenSource.Token);
                    postViewModel.IsAlreadyUploaded = true;
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

        // --- REFACTORED: This method now implements the new, inverted application logic ---
        private async Task LoadTopPostsAsync()
        {
            try
            {
                _fetchedPosts.Clear();
                var allDestinations = _serviceProvider.GetRequiredService<IEnumerable<IVideoDestination>>();
                var authenticatedDestinations = allDestinations.Where(d => d.IsAuthenticated).ToList();

                // Step 1: Check if at least one platform is authenticated.
                if (!authenticatedDestinations.Any())
                {
                    _logger.LogWarning("No authenticated platforms found. Prompting user to configure settings.");
                    MessageBox.Show("No social media accounts are connected. Please go to Settings to connect at least one account before fetching posts.", "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return; // Stop the process
                }

                _logger.LogInformation("Authenticated platforms found: {Platforms}", string.Join(", ", authenticatedDestinations.Select(d => d.Name)));

                // Step 2: Get all previously uploaded video titles from all connected platforms.
                var allUploadedTitles = new HashSet<string>();
                foreach (var destination in authenticatedDestinations)
                {
                    var titles = await destination.GetUploadedVideoTitlesAsync(CancellationToken.None);
                    allUploadedTitles.UnionWith(titles); // Combine all titles into a single set
                }
                _logger.LogInformation("Found {Count} unique video titles across all connected platforms.", allUploadedTitles.Count);

                // Step 3: Load posts from Reddit.
                _logger.LogInformation("Fetching top Reddit posts...");
                var posts = await _redditService.FetchFullPostDataAsync(CancellationToken.None);

                // Step 4: Check for duplication and populate the UI.
                await Dispatcher.InvokeAsync(() =>
                {
                    foreach (var post in posts)
                    {
                        var sanitizedTitle = TextUtils.SanitizeYouTubeTitle($"(r/{post.Subreddit}) - {post.Title ?? "Reddit Story"}");
                        _fetchedPosts.Add(new RedditPostViewModel
                        {
                            Id = post.Id,
                            Title = post.Title,
                            Score = post.Score,
                            Subreddit = post.Subreddit,
                            Url = post.Url,
                            Permalink = post.Permalink,
                            Comments = post.Comments.ToList(),
                            IsAlreadyUploaded = allUploadedTitles.Contains(sanitizedTitle)
                        });
                    }
                });
                _logger.LogInformation("Loaded {Count} posts. Marked {UploadedCount} as already uploaded.", _fetchedPosts.Count, _fetchedPosts.Count(p => p.IsAlreadyUploaded));
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
            // After settings are saved, reload everything with the new logic
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