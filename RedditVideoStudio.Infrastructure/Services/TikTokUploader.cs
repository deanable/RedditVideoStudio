namespace RedditVideoStudio.Infrastructure.Services
{
    using Flurl.Http;
    using Microsoft.Extensions.Logging;
    using RedditVideoStudio.Core.Exceptions;
    using RedditVideoStudio.Core.Interfaces;
    using RedditVideoStudio.Domain.Models; // Ensure this is included
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

        // CORRECTED: Method signature now accepts VideoDetails
        public async Task UploadVideoAsync(string videoPath, VideoDetails videoDetails, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initiating TikTok upload for '{Title}'.", videoDetails.Title);

            // CORRECTED: The post_info object now includes the mandatory privacy_level.
            var initRequestBody = new
            {
                post_info = new
                {
                    title = videoDetails.Title,
                    privacy_level = videoDetails.IsPrivate ? "SELF_ONLY" : "PUBLIC_TO_EVERYONE",
                    disable_comment = false, // Sensible default
                    disable_duet = false,    // Sensible default
                    disable_stitch = false,  // Sensible default
                },
                source_info = new
                {
                    source = "FILE_UPLOAD",
                    video_size = new FileInfo(videoPath).Length
                }
            };

            try
            {
                // 1. Initialize the upload
                var initResponse = await "https://open.tiktokapis.com/v2/post/publish/video/init/"
                    .WithOAuthBearerToken(_accessToken)
                    .PostJsonAsync(initRequestBody, cancellationToken: cancellationToken)
                    .ReceiveJson<TikTokApiResponse<TikTokInitData>>();

                // This robust error check will now catch any API errors
                if (initResponse?.Error?.Code != "ok" || string.IsNullOrWhiteSpace(initResponse?.Data?.UploadUrl))
                {
                    var errorMessage = $"Failed to get upload URL from TikTok. API returned error: '{initResponse?.Error?.Message}' (Code: {initResponse?.Error?.Code}, Log ID: {initResponse?.Error?.LogId})";
                    _logger.LogError(errorMessage);
                    throw new ApiException(errorMessage);
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
            }
            catch (FlurlHttpException ex)
            {
                var error = await ex.GetResponseStringAsync();
                _logger.LogError(ex, "Failed to upload to TikTok. Response: {Error}", error);
                throw new ApiException($"Failed to upload to TikTok: {error}", ex);
            }
        }

        // DTOs remain the same as the previous fix
        private class TikTokApiResponse<T>
        {
            [JsonPropertyName("data")]
            public T? Data { get; set; }
            [JsonPropertyName("error")]
            public TikTokErrorData? Error { get; set; }
        }
        private class TikTokInitData
        {
            [JsonPropertyName("upload_url")]
            public string? UploadUrl { get; set; }
            [JsonPropertyName("publish_id")]
            public string? PublishId { get; set; }
        }
        private class TikTokErrorData
        {
            [JsonPropertyName("code")]
            public string Code { get; init; } = string.Empty;
            [JsonPropertyName("message")]
            public string Message { get; init; } = string.Empty;
            [JsonPropertyName("log_id")]
            public string LogId { get; init; } = string.Empty;
        }
    }
}