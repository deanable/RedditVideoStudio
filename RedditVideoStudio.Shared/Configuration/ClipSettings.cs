namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains settings for optional video clips that can be added
    /// to the beginning, middle, or end of a generated video.
    /// </summary>
    public class ClipSettings
    {
        public string IntroPath { get; set; } = string.Empty;
        public string OutroPath { get; set; } = string.Empty;
        public string BreakClipPath { get; set; } = string.Empty;
        public double IntroDuration { get; set; } = 0;
        public double BreakClipDuration { get; set; } = 0;
        public double OutroDuration { get; set; } = 0;
    }
}
