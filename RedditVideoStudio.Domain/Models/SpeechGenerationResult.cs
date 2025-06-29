using System;

namespace RedditVideoStudio.Domain.Models
{
    /// <summary>
    /// Represents the result of a text-to-speech generation operation,
    /// containing the path to the output file and its precise duration.
    /// </summary>
    public record SpeechGenerationResult(string FilePath, TimeSpan Duration);
}