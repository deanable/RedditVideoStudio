using System.IO;
using System.Linq;

namespace RedditVideoStudio.Shared.Utilities
{
    /// <summary>
    /// Provides utility methods for file and path manipulation.
    /// </summary>
    public static class FileUtils
    {
        /// <summary>
        /// Sanitizes a string by replacing characters that are invalid in file names
        /// with an underscore.
        /// </summary>
        /// <param name="input">The string to sanitize.</param>
        /// <returns>A sanitized string that is safe to use as a file name.</returns>
        public static string SanitizeFileName(string input)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(input.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        }
    }
}
