using System;

namespace RedditVideoStudio.Core.Exceptions
{
    /// <summary>
    /// Represents errors that occur during Text-to-Speech (TTS) generation.
    /// </summary>
    public class TtsException : Exception
    {
        public TtsException(string message) : base(message) { }
        public TtsException(string message, Exception innerException) : base(message, innerException) { }
    }
}
