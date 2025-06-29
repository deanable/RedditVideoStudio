namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// A container for all application settings, organized by feature area.
    /// This class provides a centralized and strongly-typed way to access
    /// configuration values throughout the application.
    /// </summary>
    public class AppSettings
    {
        public RedditSettings Reddit { get; set; } = new();
        public PexelsSettings Pexels { get; set; } = new();
        public TtsSettings Tts { get; set; } = new();
        public FfmpegSettings Ffmpeg { get; set; } = new();
        public YouTubeSettings YouTube { get; set; } = new();
        public ImageGenerationSettings ImageGeneration { get; set; } = new();
        public GoogleCloudSettings GoogleCloud { get; set; } = new();
        public ClipSettings ClipSettings { get; set; } = new();
        public AzureTtsSettings AzureTts { get; set; } = new();
        public ElevenLabsSettings ElevenLabs { get; set; } = new(); // Added
    }
}