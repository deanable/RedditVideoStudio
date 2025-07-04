// In: C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\TikTokVideoDestination.cs
namespace RedditVideoStudio.Infrastructure.Services
{
    using Google.Protobuf.WellKnownTypes;
    using RedditVideoStudio.Core.Interfaces;
    using RedditVideoStudio.Domain.Models;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using static Google.Protobuf.Compiler.CodeGeneratorResponse.Types;

    /// <summary>
    /// A placeholder implementation for a TikTok video destination.
    /// This service is registered but not fully functional, similar to the Instagram placeholder.
    /// </summary>
    public class TikTokVideoDestination : IVideoDestination
    {
        /// <summary>
        /// Gets the name of the destination.
        /// </summary>
        public string Name => "TikTok";

        /// <summary>
        /// Gets a value indicating whether the user is authenticated.This is always false for the placeholder.
        /// </summary>
        public bool IsAuthenticated => false;

        /// <summary>
        /// Throws NotImplementedException because this feature is not available.
        /// </summary>
        public Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("TikTok uploading is not yet supported in this version.");
        }

        /// <summary>
        /// Throws NotImplementedException because this feature is not available.
        /// </summary>
        public Task SignOutAsync()
        {
            throw new NotImplementedException("TikTok uploading is not yet supported in this version.");
        }

        /// <summary>
        /// Throws NotImplementedException because this feature is not available.
        /// </summary>
        public Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, string? thumbnailPath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("TikTok uploading is not yet supported in this version.");
        }

        // In TikTokVideoDestination.cs (inside TikTokDestination.cs)
        public Task<HashSet<string>> GetUploadedVideoTitlesAsync(CancellationToken cancellationToken = default)
        {
            // This is a placeholder; real implementation would require TikTok API calls.
            return Task.FromResult(new HashSet<string>());
        }

        public Task<bool> DoesVideoExistAsync(string title, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}