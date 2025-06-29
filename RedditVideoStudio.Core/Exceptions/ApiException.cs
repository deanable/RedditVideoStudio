using System;

namespace RedditVideoStudio.Core.Exceptions
{
    /// <summary>
    /// Base exception for API-related errors.
    /// </summary>
    public class ApiException : Exception
    {
        public ApiException(string message) : base(message) { }
        public ApiException(string message, Exception innerException) : base(message, innerException) { }
    }
}
