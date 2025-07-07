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
                // CORRECTED: Deserialize into a more robust DTO that includes the 'error' field.
                var initResponse = await "https://open.tiktokapis.com/v2/post/publish/video/init/"
                    .WithOAuthBearerToken(_accessToken)
                    .PostJsonAsync(initRequestBody, cancellationToken: cancellationToken)
                    .ReceiveJson<TikTokApiResponse<TikTokInitData>>();

                // CORRECTED: More robust error checking.
                // First, check for a non-"ok" error code from the API body.
                // Then, ensure the UploadUrl is actually present.
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
                // Note: TikTok processes the video asynchronously. No further action is needed here.
            }
            catch (FlurlHttpException ex)
            {
                var error = await ex.GetResponseStringAsync();
                _logger.LogError(ex, "Failed to upload to TikTok. Response: {Error}", error);
                throw new ApiException($"Failed to upload to TikTok: {error}", ex);
            }
        }

        // --- DTOs Revised for Robustness ---

        /// <summary>
        /// A generic wrapper for TikTok API responses, containing both data and error fields.
        /// </summary>
        private class TikTokApiResponse<T>
        {
            [JsonPropertyName("data")]
            public T? Data { get; set; }

            [JsonPropertyName("error")]
            public TikTokErrorData? Error { get; set; }
        }

        /// <summary>
        /// Represents the data object from the /video/init/ response.
        /// </summary>
        private class TikTokInitData
        {
            [JsonPropertyName("upload_url")]
            public string? UploadUrl { get; set; }
            [JsonPropertyName("publish_id")]
            public string? PublishId { get; set; }
        }

        /// <summary>
        /// Represents the error object in a standard TikTok API response.
        /// </summary>
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