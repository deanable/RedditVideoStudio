namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Holds all settings related to the generation of images, including
    /// text overlays for video segments and the final video thumbnail.
    /// </summary>
    public class ImageGenerationSettings
    {
        public int InteriorPadding { get; set; } = 50;
        public int ExteriorPadding { get; set; } = 100;
        public int FontSize { get; set; } = 48;
        public string FontFamily { get; set; } = "Arial";
        public double BackgroundOpacity { get; set; } = 0.6;
        public string RectangleColor { get; set; } = "#000000";
        public string TextColor { get; set; } = "#FFFFFF";
        public int MaxCharactersPerPage { get; set; } = 400;
        public int ImageWidth { get; set; } = 1920;
        public int ImageHeight { get; set; } = 1080;
        public string ThumbnailBackgroundColor { get; set; } = "#1A1A1B";
        public string ThumbnailPexelsQuery { get; set; } = "abstract background";
        public int ThumbnailFontSize { get; set; } = 120;
        public int ThumbnailFontOutlineWidth { get; set; } = 8;
        public string ThumbnailFontOutlineColor { get; set; } = "#000000";
    }
}
