using System;

namespace RedditVideoStudio.Core.Exceptions
{
    /// <summary>
    /// Represents errors that occur while interacting with the Pexels API.
    /// </summary>
    public class PexelsApiException : ApiException
    {
        public PexelsApiException(string message) : base(message) { }
        public PexelsApiException(string message, Exception innerException) : base(message, innerException) { }
    }
}
