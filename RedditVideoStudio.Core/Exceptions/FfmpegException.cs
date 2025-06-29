using System;

namespace RedditVideoStudio.Core.Exceptions
{
    /// <summary>
    /// Represents errors that occur during the execution of an FFmpeg process.
    /// This exception includes the exit code and any captured error output from FFmpeg,
    /// providing detailed information for debugging rendering or processing failures.
    /// </summary>
    public class FfmpegException : Exception
    {
        public int ExitCode { get; }
        public string FfmpegErrorOutput { get; }

        public FfmpegException(string message) : base(message)
        {
            ExitCode = -1;
            FfmpegErrorOutput = string.Empty;
        }

        public FfmpegException(string message, Exception innerException) : base(message, innerException)
        {
            ExitCode = -1;
            FfmpegErrorOutput = string.Empty;
        }

        public FfmpegException(string message, int exitCode, string errorOutput, Exception? innerException)
            : base(message, innerException)
        {
            ExitCode = exitCode;
            FfmpegErrorOutput = errorOutput;
        }
    }
}
