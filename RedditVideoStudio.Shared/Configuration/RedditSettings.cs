namespace RedditVideoStudio.Shared.Configuration
{
    /// <summary>
    /// Contains settings specific to interacting with the Reddit API,
    /// such as the target subreddit and limits for fetching posts and comments.
    /// </summary>
    public class RedditSettings
    {
        public string Subreddit { get; set; } = "askreddit";
        public int PostLimit { get; set; } = 5;
        public int CommentLimit { get; set; } = 10;
    }
}
