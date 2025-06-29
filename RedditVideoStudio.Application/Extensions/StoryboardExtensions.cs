using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Application.Extensions
{
    /// <summary>
    /// Provides extension methods for the Storyboard class to add convenient,
    /// higher-level functionality.
    /// </summary>
    public static class StoryboardExtensions
    {
        /// <summary>
        /// Saves all audio tracks from the storyboard's items into a single,
        /// concatenated audio file using the provided FFmpeg service.
        /// </summary>
        /// <param name="storyboard">The storyboard containing the audio items.</param>
        /// <param name="outputPath">The path to save the concatenated audio file.</param>
        /// <param name="ffmpegService">The FFmpeg service to use for concatenation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public static async Task SaveConcatenatedAudioAsync(this Storyboard storyboard, string outputPath, IFfmpegService ffmpegService, CancellationToken cancellationToken = default)
        {
            var audioPaths = storyboard.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.AudioPath))
                .Select(i => i.AudioPath!)
                .ToList();

            if (!audioPaths.Any())
            {
                throw new InvalidOperationException("No audio files to concatenate.");
            }

            await ffmpegService.ConcatenateAudioAsync(audioPaths, outputPath, cancellationToken);
        }
    }
}
