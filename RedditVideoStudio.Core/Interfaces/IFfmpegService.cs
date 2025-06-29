using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that interacts with the FFmpeg command-line tool
    /// to perform video and audio processing tasks.
    /// </summary>
    public interface IFfmpegService
    {
        /// <summary>
        /// Renders a final video by combining a background video, an optional narration audio track,
        /// and a storyboard of image overlays.
        /// </summary>
        // CORRECTED: Added the 'TimeSpan videoDuration' parameter to the contract.
        Task RenderFinalVideoAsync(string backgroundPath, string? narrationPath, Storyboard storyboard, TimeSpan videoDuration, IProgress<ProgressReport> progress, string outputPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Concatenates multiple audio files into a single audio file.
        /// </summary>
        Task ConcatenateAudioAsync(IEnumerable<string> audioPaths, string outputPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously gets the duration of a video file.
        /// </summary>
        Task<TimeSpan> GetVideoDurationAsync(string videoFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Concatenates multiple video files into a single video file.
        /// </summary>
        Task ConcatenateVideosAsync(IEnumerable<string> videoPaths, string outputPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Trims a video to a specified duration.
        /// </summary>
        Task<string> TrimVideoAsync(string inputPath, TimeSpan duration, string outputPath, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if a video file contains an audio stream.
        /// </summary>
        Task<bool> HasAudioStreamAsync(string videoPath, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a silent audio file for a given duration.
        /// </summary>
        Task<string> CreateSilentAudioAsync(TimeSpan duration, string outputPath, CancellationToken cancellationToken);

        /// <summary>
        /// Merges a video stream and an audio stream into a single output file without re-encoding the video.
        /// </summary>
        Task<string> MergeAudioAndVideoAsync(string videoPath, string audioPath, string outputPath, CancellationToken cancellationToken);

        /// <summary>
        /// Normalizes a video clip to project standards for codec, resolution, and audio. Adds a silent audio track if one is missing.
        /// </summary>
        Task<string> NormalizeVideoAsync(string inputPath, string outputPath, CancellationToken cancellationToken);

        /// <summary>
        /// Converts a WAV audio file to MP3 format.
        /// </summary>
        Task<string> ConvertWavToMp3Async(string inputWavPath, string outputMp3Path, CancellationToken cancellationToken);
    }
}