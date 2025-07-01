using System.Collections.Generic;

namespace RedditVideoStudio.Shared.Configuration
{
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
        public ElevenLabsSettings ElevenLabs { get; set; } = new();
        public TikTokSettings TikTok { get; set; } = new();
        public Dictionary<string, bool> EnabledDestinations { get; set; } = new();
    }
}