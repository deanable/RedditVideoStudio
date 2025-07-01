using Flurl.Http;
using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    public class TikTokUploader : ITikTokUploadService
    {
        private readonly ILogger<TikTokUploader> _logger;
        private readonly string _accessToken;

        public TikTokUploader(ILogger<TikTokUploader> logger, string accessToken)
        {
            _logger = logger;
            _accessToken = accessToken;
        }

        public async Task UploadVideoAsync(string videoPath, string title, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("TikTokUploader: Starting upload process for '{Title}'.", title);

            var requestBody = new
            {
                post_info = new
                {
                    title = title,
                    privacy_level = "PUBLIC_TO_SELF",
                },
                source_info = new
                {
                    source = "FILE_UPLOAD",
                    video_size = new FileInfo(videoPath).Length,
                }
            };

            try
            {
                var response = await "https://open.tiktokapis.com/v2/post/publish/video/init/"
                    .WithOAuthBearerToken(_accessToken)
                    .PostJsonAsync(requestBody, HttpCompletionOption.ResponseContentRead, cancellationToken);

                var responseString = await response.GetStringAsync();
                _logger.LogInformation("Successfully initiated TikTok upload. Response: {Response}", responseString);
                _logger.LogWarning("TikTokUploader: Video upload is not fully implemented. The video file was NOT uploaded.");

            }
            catch (FlurlHttpException ex)
            {
                var error = await ex.GetResponseStringAsync();
                _logger.LogError(ex, "Failed to initiate TikTok upload. Response: {Error}", error);
                throw new ApiException($"Failed to initiate TikTok upload: {error}", ex);
            }
        }
    }
}