using System;
using System.IO;

namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains all settings related to the FFmpeg process, including the directory
    /// where the executables are located and parameters for video and audio encoding.
    /// </summary>
    public class FfmpegSettings
    {
        /// <summary>
        /// The orientation of the output video. Can be "Landscape" or "Portrait".
        /// </summary>
        public string VideoOrientation { get; set; } = "Landscape";

        /// <summary>
        /// The directory containing ffmpeg.exe and ffprobe.exe, relative to the application's startup directory.
        /// </summary>
        public string FfmpegDirectory { get; set; } = "ffmpeg";

        public string VideoCodec { get; set; } = "libx264";

        public string AudioCodec { get; set; } = "libmp3lame";

        public string Preset { get; set; } = "ultrafast";
        public string VideoBitrate { get; set; } = "5000k";
        public string AudioBitrate { get; set; } = "192k";

        /// <summary>
        /// A read-only property that constructs the full path to ffmpeg.exe.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string FfmpegExePath => Path.Combine(AppContext.BaseDirectory, FfmpegDirectory, "ffmpeg.exe");

        /// <summary>
        /// A read-only property that constructs the full path to ffprobe.exe.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string FfprobeExePath => Path.Combine(AppContext.BaseDirectory, FfmpegDirectory, "ffprobe.exe");
    }
}