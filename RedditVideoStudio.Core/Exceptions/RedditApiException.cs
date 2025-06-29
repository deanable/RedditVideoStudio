using System;

namespace RedditVideoStudio.Core.Exceptions
{
    /// <summary>
    /// Represents errors that occur while interacting with the Reddit API.
    /// </summary>
    public class RedditApiException : ApiException
    {
        public RedditApiException(string message) : base(message) { }
        public RedditApiException(string message, Exception innerException) : base(message, innerException) { }
    }
}
