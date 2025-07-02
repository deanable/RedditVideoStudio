namespace RedditVideoStudio.Infrastructure.Services
{
    using Flurl.Http;
    using Microsoft.Extensions.Logging;
    using RedditVideoStudio.Core.Exceptions;
    using RedditVideoStudio.Core.Interfaces;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Text.Json.Serialization;

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
            _logger.LogInformation("Initiating TikTok upload for '{Title}'.", title);

            var initRequestBody = new
            {
                post_info = new { title = title },
                source_info = new { source = "FILE_UPLOAD", video_size = new FileInfo(videoPath).Length }
            };

            try
            {
                // 1. Initialize the upload
                var initResponse = await "https://open.tiktokapis.com/v2/post/publish/video/init/"
                    .WithOAuthBearerToken(_accessToken)
                    .PostJsonAsync(initRequestBody, cancellationToken: cancellationToken)
                    .ReceiveJson<TikTokInitResponse>();

                if (initResponse?.Data?.UploadUrl == null)
                {
                    throw new ApiException("Failed to get upload URL from TikTok.");
                }

                _logger.LogInformation("TikTok upload initialized. Uploading video file to the provided URL.");

                // 2. Upload the video file
                var videoBytes = await File.ReadAllBytesAsync(videoPath, cancellationToken);
                var uploadResponse = await initResponse.Data.UploadUrl
                    .WithHeader("Content-Type", "video/mp4")
                    .PutAsync(new ByteArrayContent(videoBytes), cancellationToken: cancellationToken);

                if (!uploadResponse.ResponseMessage.IsSuccessStatusCode)
                {
                    var errorBody = await uploadResponse.ResponseMessage.Content.ReadAsStringAsync(cancellationToken);
                    throw new ApiException($"TikTok video file upload failed with status {uploadResponse.StatusCode}. Details: {errorBody}");
                }

                _logger.LogInformation("Video file successfully uploaded to TikTok's servers. Publish ID: {PublishId}", initResponse.Data.PublishId);
                // Note: TikTok processes the video asynchronously. No further action is needed here.
            }
            catch (FlurlHttpException ex)
            {
                var error = await ex.GetResponseStringAsync();
                _logger.LogError(ex, "Failed to upload to TikTok. Response: {Error}", error);
                throw new ApiException($"Failed to upload to TikTok: {error}", ex);
            }
        }

        private class TikTokInitResponse
        {
            [JsonPropertyName("data")]
            public TikTokInitData? Data { get; set; }
        }

        private class TikTokInitData
        {
            [JsonPropertyName("upload_url")]
            public string? UploadUrl { get; set; }
            [JsonPropertyName("publish_id")]
            public string? PublishId { get; set; }
        }
    }
}
