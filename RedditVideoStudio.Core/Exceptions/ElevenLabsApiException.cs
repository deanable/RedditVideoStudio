using System;

namespace RedditVideoStudio.Core.Exceptions
{
    /// <summary>
    /// Represents errors that occur while interacting with the ElevenLabs API.
    /// </summary>
    public class ElevenLabsApiException : ApiException
    {
        public ElevenLabsApiException(string message) : base(message) { }
        public ElevenLabsApiException(string message, Exception innerException) : base(message, innerException) { }
    }
}