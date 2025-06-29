using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that handles image generation and manipulation.
    /// </summary>
    public interface IImageService
    {
        /// <summary>
        /// Generates an image with the given text rendered onto it.
        /// </summary>
        /// <param name="text">The text to render on the image.</param>
        /// <param name="outputPath">The path to save the generated image.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The path to the generated image.</returns>
        Task<string> GenerateImageFromTextAsync(string text, string outputPath, CancellationToken cancellationToken);

        /// <summary>
        /// Generates a video thumbnail by rendering text over a background image.
        /// </summary>
        /// <param name="backgroundImagePath">The path to the background image for the thumbnail.</param>
        /// <param name="text">The text to render on the thumbnail.</param>
        /// <param name="outputPath">The path to save the generated thumbnail.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The path to the generated thumbnail.</returns>
        Task<string> GenerateThumbnailAsync(string backgroundImagePath, string text, string outputPath, CancellationToken cancellationToken);
    }
}
