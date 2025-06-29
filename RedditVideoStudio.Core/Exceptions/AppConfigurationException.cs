using System;

namespace RedditVideoStudio.Core.Exceptions
{
    /// <summary>
    /// Represents errors that occur during the configuration process,
    /// such as failing to load settings from the registry.
    /// </summary>
    public class AppConfigurationException : Exception
    {
        public AppConfigurationException(string message) : base(message) { }
        public AppConfigurationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
