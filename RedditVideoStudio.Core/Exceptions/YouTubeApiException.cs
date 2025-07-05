using System;

namespace RedditVideoStudio.Core.Exceptions
{
    /// <summary>
    /// Represents errors that occur while interacting with the YouTube Data API.
    /// </summary>
    public class YouTubeApiException : ApiException
    {
        public YouTubeApiException(string message) : base(message) { }
        // MODIFIED: Exception is now nullable to align with its base class.
        public YouTubeApiException(string message, Exception? innerException) : base(message, innerException) { }
    }
}