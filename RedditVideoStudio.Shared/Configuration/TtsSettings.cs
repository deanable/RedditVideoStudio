namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains settings for the Text-to-Speech (TTS) services.
    /// This allows for configuring which TTS provider is used, as well as
    /// voice characteristics like language, gender, and speaking rate.
    /// </summary>
    public class TtsSettings
    {
        public string Provider { get; set; } = "Google";
        public string LanguageCode { get; set; } = "en-US";
        public string VoiceGender { get; set; } = "Female";
        public double SpeakingRate { get; set; } = 1.0;
        public string WindowsVoice { get; set; } = "Microsoft David Desktop";

    }
}
