// The main entry point for the application, handling startup, service configuration, and shutdown.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Application;
using RedditVideoStudio.Application.Services;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Infrastructure.Services;
using RedditVideoStudio.UI.Logging;
using RedditVideoStudio.UI.ViewModels;
using RedditVideoStudio.UI.ViewModels.Settings;
using Serilog;
using System;
using System.IO;
using System.Windows;

namespace RedditVideoStudio.UI
{
    /// <summary>
    /// Main application class. Handles startup, DI configuration, and shutdown.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        /// <summary>
        /// The main application host, containing all registered services.
        /// </summary>
        public static IHost? Host { get; private set; }

        /// <summary>
        /// Handles the application startup event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The startup event arguments.</param>
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Configure the global logger for the application.
                ConfigureLogger();

                // Build and configure the application host and its services.
                Host = ConfigureHost();

                // Start the host asynchronously.
                await Host.StartAsync();

                // Retrieve the main window from the service provider and show it.
                Host.Services.GetRequiredService<MainWindow>().Show();
            }
            catch (Exception ex)
            {
                // Catch and log any catastrophic failures during startup.
                MessageBox.Show($"A critical error occurred on startup: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Log.Fatal(ex, "Host initialization failed catastrophically.");
            }
        }

        /// <summary>
        /// Configures the dependency injection container with all the application's services.
        /// </summary>
        /// <returns>A configured IHost instance.</returns>
        private IHost ConfigureHost()
        {
            return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseSerilog() // Use Serilog for logging.
                .ConfigureServices((context, services) =>
                {
                    // =========================================================================
                    // This is the fix for the startup error.
                    // The AddHttpClient() extension method registers the IHttpClientFactory
                    // and related services in the dependency injection container. This is
                    // required by services like TikTokAuthService that need to make HTTP requests.
                    // =========================================================================
                    services.AddHttpClient();

                    // Register MediatR for handling in-process messaging.
                    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyReference).Assembly));

                    // Register all video destination platforms as singletons.
                    services.AddSingleton<IVideoDestination, YouTubeDestination>();
                    services.AddSingleton<IVideoDestination, TikTokDestination>();
                    services.AddSingleton<IVideoDestination, InstagramDestination>();

                    // Register core application and infrastructure services as singletons.
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<IAppConfiguration, RegistryAppConfiguration>();
                    services.AddSingleton<IRedditService, RedditService>();
                    services.AddSingleton<IPexelsService, PexelsService>();
                    services.AddSingleton<IImageService, OverlayGeneratorService>();
                    services.AddSingleton<IAudioUtility, AudioUtility>();
                    services.AddSingleton<IFfmpegService, FfmpegService>();
                    services.AddSingleton<IYouTubeServiceFactory, YouTubeServiceFactory>();
                    services.AddSingleton<ITempDirectoryFactory, TempDirectoryFactory>();
                    services.AddSingleton<ITikTokServiceFactory, TikTokServiceFactory>();
                    services.AddSingleton<ITikTokAuthService, TikTokAuthService>();

                    // Register all available Text-to-Speech services as singletons.
                    services.AddSingleton<GoogleTextToSpeechService>();
                    services.AddSingleton<AzureTextToSpeechService>();
                    services.AddSingleton<ElevenLabsTextToSpeechService>();
                    services.AddSingleton<WindowsTextToSpeechService>();

                    // Services that perform a single, complete operation (like composing a video)
                    // are registered as Transient. This ensures a fresh instance is created for each operation.
                    services.AddTransient<IStoryboardGenerator, StoryboardGenerator>();
                    services.AddTransient<IVideoSegmentGenerator, VideoSegmentGenerator>();
                    services.AddTransient<IVideoComposer, VideoComposer>();
                    services.AddTransient<IPublishingService, PublishingService>();

                    // Register the FFmpeg downloader service.
                    services.AddSingleton<IFfmpegDownloaderService, FfmpegDownloaderService>();

                    // Register a factory to dynamically select the TTS provider based on settings.
                    services.AddTransient<ITextToSpeechService>(serviceProvider =>
                    {
                        var configService = serviceProvider.GetRequiredService<IAppConfiguration>();
                        var provider = configService.Settings.Tts.Provider;
                        switch (provider?.ToLower())
                        {
                            case "azure":
                                return serviceProvider.GetRequiredService<AzureTextToSpeechService>();
                            case "windows":
                                return serviceProvider.GetRequiredService<WindowsTextToSpeechService>();
                            case "elevenlabs":
                                return serviceProvider.GetRequiredService<ElevenLabsTextToSpeechService>();
                            default: // "google" is the default
                                return serviceProvider.GetRequiredService<GoogleTextToSpeechService>();
                        }
                    });

                    // Register ViewModels and Windows for the UI layer.
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<DestinationsSettingsViewModel>();
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<SettingsWindow>();
                }).Build();
        }

        /// <summary>
        /// Configures the Serilog logger to write to a file and the UI.
        /// </summary>
        private void ConfigureLogger()
        {
            string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory); // Ensure the directory exists.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug() // Set the minimum level to log.
                .WriteTo.File(Path.Combine(logDirectory, $"ui-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt"), rollingInterval: RollingInterval.Day) // Log to a daily file.
                .WriteTo.Sink(new DelegatingSink(), Serilog.Events.LogEventLevel.Information) // Log to the UI via a delegating sink.
                .CreateLogger();
        }

        /// <summary>
        /// Handles the application exit event to gracefully shut down the host.
        /// </summary>
        private async void Application_Exit(object sender, ExitEventArgs e)
        {
            if (Host != null)
            {
                await Host.StopAsync();
                Host.Dispose();
            }
            Log.CloseAndFlush(); // Ensure all logs are written.
        }
    }
}