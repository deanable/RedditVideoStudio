namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains settings specific to the ElevenLabs Text-to-Speech service.
    /// </summary>
    public class ElevenLabsSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string VoiceId { get; set; } = "21m00Tcm4TlvDq8ikWAM"; // A default voice ID
        public string ModelId { get; set; } = "eleven_multilingual_v2";
    }
}