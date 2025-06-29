using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the IRedditService interface to fetch post and comment data from the Reddit API.
    /// </summary>
    public class RedditService : IRedditService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RedditService> _logger;
        private readonly IAppConfiguration _appConfig;

        public RedditService(IAppConfiguration appConfig, ILogger<RedditService> logger)
        {
            _appConfig = appConfig;
            _logger = logger;
            _httpClient = new HttpClient();
            // It's good practice to set a custom User-Agent when interacting with APIs.
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RedditVideoStudio/1.0");
        }

        public async Task<List<RedditPostData>> FetchFullPostDataAsync(CancellationToken cancellationToken)
        {
            var redditSettings = _appConfig.Settings.Reddit;
            var url = $"https://www.reddit.com/r/{redditSettings.Subreddit}/top.json?limit={redditSettings.PostLimit}";
            var result = new List<RedditPostData>();

            try
            {
                _logger.LogInformation("Fetching top {PostLimit} posts from r/{Subreddit}", redditSettings.PostLimit, redditSettings.Subreddit);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var jsonDoc = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
                var posts = jsonDoc.RootElement.GetProperty("data").GetProperty("children").EnumerateArray();

                foreach (var post in posts)
                {
                    var data = post.GetProperty("data");
                    var postId = data.GetProperty("id").GetString() ?? string.Empty;
                    _logger.LogInformation("Fetching top {CommentLimit} comments for post ID: {PostId}", redditSettings.CommentLimit, postId);

                    var commentsUrl = $"https://www.reddit.com/comments/{postId}.json?limit={redditSettings.CommentLimit}";
                    var commentResponse = await _httpClient.GetAsync(commentsUrl, cancellationToken);
                    commentResponse.EnsureSuccessStatusCode();

                    using var commentContentStream = await commentResponse.Content.ReadAsStreamAsync(cancellationToken);
                    var commentDoc = await JsonDocument.ParseAsync(commentContentStream, cancellationToken: cancellationToken);
                    var comments = new List<string>();

                    var commentListings = commentDoc.RootElement[1].GetProperty("data").GetProperty("children").EnumerateArray();
                    foreach (var commentItem in commentListings)
                    {
                        if (commentItem.TryGetProperty("data", out var commentData) && commentData.TryGetProperty("body", out var body))
                        {
                            var commentText = body.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(commentText) &&
                                !commentText.Contains("[deleted]", StringComparison.OrdinalIgnoreCase) &&
                                !commentText.Contains("[removed]", StringComparison.OrdinalIgnoreCase))
                            {
                                comments.Add(commentText);
                            }
                        }
                    }

                    result.Add(new RedditPostData
                    {
                        Id = postId,
                        Title = data.GetProperty("title").GetString() ?? "",
                        Subreddit = data.GetProperty("subreddit").GetString() ?? "",
                        Url = data.GetProperty("url").GetString() ?? "",
                        Permalink = data.GetProperty("permalink").GetString() ?? "",
                        Score = data.TryGetProperty("score", out var scoreVal) ? scoreVal.GetInt32() : 0,
                        Comments = comments
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch full Reddit post data for subreddit {Subreddit}", redditSettings.Subreddit);
                throw new RedditApiException($"Failed to fetch data from Reddit. Check network connection and subreddit name.", ex);
            }
        }
    }
}
