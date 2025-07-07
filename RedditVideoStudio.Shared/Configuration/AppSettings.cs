using System.Collections.Generic;

namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// The main container for all application settings.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Gets or sets the settings related to the Reddit API.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public RedditSettings Reddit { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings for the Pexels API (for background videos).
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public PexelsSettings Pexels { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings for Text-to-Speech services.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public TtsSettings Tts { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings for FFmpeg encoding.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public FfmpegSettings Ffmpeg { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings related to YouTube uploads.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public YouTubeSettings YouTube { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings for generating image overlays.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public ImageGenerationSettings ImageGeneration { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings for Google Cloud services.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public GoogleCloudSettings GoogleCloud { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings for intro/outro video clips.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public ClipSettings ClipSettings { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings for the Azure TTS service.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public AzureTtsSettings AzureTts { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings for the ElevenLabs TTS service.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public ElevenLabsSettings ElevenLabs { get; set; } = new();

        /// <summary>
        /// Gets or sets the settings for the TikTok API.
        /// CORRECTED: This property must be initialized to prevent NullReferenceException on startup.
        /// </summary>
        public TikTokSettings TikTok { get; set; } = new();

        /// <summary>
        /// Gets or sets the dictionary of enabled publishing destinations.
        /// It is initialized to a new instance to prevent null reference exceptions.
        /// </summary>
        public Dictionary<string, bool> EnabledDestinations { get; set; } = new();
    }
}