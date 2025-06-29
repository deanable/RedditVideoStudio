// C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.UI\App.xaml.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Application.Services;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Infrastructure.Services;
using RedditVideoStudio.UI.Logging;
using RedditVideoStudio.UI.ViewModels;
using RedditVideoStudio.Infrastructure.Services;
using Serilog;
using System;
using System.IO;
using System.Windows;

namespace RedditVideoStudio.UI
{
    /// <summary>
    /// The main entry point for the WPF application. This class is responsible for
    /// configuring and starting the application host, which includes setting up
    /// dependency injection, logging, and launching the main window.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public static IHost? Host { get; private set; }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Ensure the logs directory exists before setting up the logger
                string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDirectory);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(Path.Combine(logDirectory, $"ui-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt"), rollingInterval: RollingInterval.Day)
                    .WriteTo.Sink(new DelegatingSink(), Serilog.Events.LogEventLevel.Information)
                    .CreateLogger();

                Log.Information("Logger configured. Starting host builder...");
                Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureServices((context, services) =>
                    {
                        // Configuration Service
                        services.AddSingleton<IAppConfiguration, RegistryAppConfiguration>();

                        // Application Layer Services
                        services.AddSingleton<IStoryboardGenerator, StoryboardGenerator>();
                        services.AddSingleton<IVideoSegmentGenerator, VideoSegmentGenerator>();
                        services.AddSingleton<IVideoComposer, VideoComposer>();

                        // Infrastructure Services
                        services.AddSingleton<IRedditService, RedditService>();
                        services.AddSingleton<IPexelsService, PexelsService>();
                        services.AddSingleton<IImageService, OverlayGeneratorService>();
                        services.AddSingleton<IAudioUtility, AudioUtility>();
                        services.AddSingleton<IFfmpegService, FfmpegService>();

                        // Factories
                        services.AddSingleton<IYouTubeServiceFactory, YouTubeServiceFactory>();
                        services.AddSingleton<ITempDirectoryFactory, TempDirectoryFactory>();

                        // TTS Services (for the factory)
                        services.AddSingleton<GoogleTextToSpeechService>();
                        services.AddSingleton<AzureTextToSpeechService>();
                        services.AddSingleton<ElevenLabsTextToSpeechService>();
                        // CORRECTED: The class is named WindowsTextToSpeechService, not WindowsSpeechSynthesizerService.
                        services.AddSingleton<WindowsTextToSpeechService>();


                        // TTS Service Factory
                        // A Transient lifetime ensures that a new instance of the TTS service is created
                        // every time it is requested. This is crucial because it forces the factory
                        // to re-evaluate the TTS.Provider setting from the configuration,
                        // thus respecting any changes made by the user in the Settings window.
                        services.AddTransient<ITextToSpeechService>(serviceProvider =>
                        {
                            var configService = serviceProvider.GetRequiredService<IAppConfiguration>();
                            var logger = serviceProvider.GetRequiredService<ILogger<ITextToSpeechService>>();
                            var provider = configService.Settings.Tts.Provider;

                            logger.LogInformation("Selected TTS Provider: {Provider}", provider);

                            switch (provider?.ToLower())
                            {
                                case "azure":
                                    return serviceProvider.GetRequiredService<AzureTextToSpeechService>();
                                case "windows":
                                    // CORRECTED: The container now correctly provides all dependencies for WindowsTextToSpeechService.
                                    return serviceProvider.GetRequiredService<WindowsTextToSpeechService>();
                                case "elevenlabs":
                                    return serviceProvider.GetRequiredService<ElevenLabsTextToSpeechService>();
                                default:
                                    return serviceProvider.GetRequiredService<GoogleTextToSpeechService>();
                            }
                        });
                        // UI and ViewModels
                        services.AddSingleton<SettingsViewModel>();
                        services.AddSingleton<MainWindow>();
                    }).Build();

                Log.Information("Host built successfully. Starting host...");
                await Host.StartAsync();
                Log.Information("Host started successfully.");

                var mainWindow = Host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                // This is a global catch-all for any startup exceptions.
                // It will show a message box even if the logger fails.
                string errorMessage = $"A critical error occurred on startup: {ex.Message}\n\n{ex.StackTrace}";
                MessageBox.Show(errorMessage, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Log.Fatal(ex, "Host initialization failed catastrophically.");
            }
        }

        private async void Application_Exit(object sender, ExitEventArgs e)
        {
            if (Host != null)
            {
                await Host.StopAsync();
                Host.Dispose();
            }
            Log.CloseAndFlush();
        }
    }
}