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
using System.IO;
using System.Windows;

namespace RedditVideoStudio.UI
{
    public partial class App : System.Windows.Application
    {
        public static IHost? Host { get; private set; }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                ConfigureLogger();
                Host = ConfigureHost();
                await Host.StartAsync();
                Host.Services.GetRequiredService<MainWindow>().Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"A critical error occurred on startup: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Log.Fatal(ex, "Host initialization failed catastrophically.");
            }
        }

        private IHost ConfigureHost()
        {
            return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    // This section now includes all of your original services plus our new ones.
                    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyReference).Assembly));

                    // New/Refactored Services
                    services.AddSingleton<IVideoDestination, YouTubeDestination>();
                    services.AddSingleton<ISettingsService, SettingsService>();

                    // Your Original Services (preserved)
                    services.AddSingleton<IAppConfiguration, RegistryAppConfiguration>();
                    services.AddSingleton<IRedditService, RedditService>();
                    services.AddSingleton<IPexelsService, PexelsService>();
                    services.AddSingleton<IImageService, OverlayGeneratorService>();
                    services.AddSingleton<IAudioUtility, AudioUtility>();
                    services.AddSingleton<IFfmpegService, FfmpegService>();
                    services.AddSingleton<IYouTubeServiceFactory, YouTubeServiceFactory>();
                    services.AddSingleton<ITempDirectoryFactory, TempDirectoryFactory>();
                    services.AddSingleton<TikTokAuthService>();
                    services.AddSingleton<GoogleTextToSpeechService>();
                    services.AddSingleton<AzureTextToSpeechService>();
                    services.AddSingleton<ElevenLabsTextToSpeechService>();
                    services.AddSingleton<WindowsTextToSpeechService>();
                    services.AddTransient<IStoryboardGenerator, StoryboardGenerator>();
                    services.AddTransient<IVideoSegmentGenerator, VideoSegmentGenerator>();
                    services.AddSingleton<IVideoComposer, VideoComposer>();

                    // Your TTS Factory
                    services.AddTransient<ITextToSpeechService>(serviceProvider =>
                    {
                        var configService = serviceProvider.GetRequiredService<IAppConfiguration>();
                        var logger = serviceProvider.GetRequiredService<ILogger<ITextToSpeechService>>();
                        var provider = configService.Settings.Tts.Provider;
                        switch (provider?.ToLower())
                        {
                            case "azure":
                                return serviceProvider.GetRequiredService<AzureTextToSpeechService>();
                            case "windows":
                                return serviceProvider.GetRequiredService<WindowsTextToSpeechService>();
                            case "elevenlabs":
                                return serviceProvider.GetRequiredService<ElevenLabsTextToSpeechService>();
                            default:
                                return serviceProvider.GetRequiredService<GoogleTextToSpeechService>();
                        }
                    });

                    // ViewModels
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<DestinationsSettingsViewModel>();

                    // UI Windows
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<SettingsWindow>();
                }).Build();
        }

        private void ConfigureLogger()
        {
            string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(logDirectory, $"ui-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt"), rollingInterval: RollingInterval.Day)
                .WriteTo.Sink(new DelegatingSink(), Serilog.Events.LogEventLevel.Information)
                .CreateLogger();
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