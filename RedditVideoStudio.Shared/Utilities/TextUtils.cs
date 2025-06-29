using System;
using System.Collections.Generic;
using System.Net; // Add this using statement
using System.Text;
using System.Text.RegularExpressions;

namespace RedditVideoStudio.Shared.Utilities
{
    /// <summary>
    /// Provides utility methods for text manipulation.
    /// </summary>
    public static class TextUtils
    {
        /// <summary>
        /// Cleans text content by removing hyperlinks, emojis, decoding HTML entities, and trimming extra whitespace.
        /// </summary>
        /// <param name="text">The input text to sanitize.</param>
        /// <returns>Sanitized text.</returns>
        public static string SanitizePostContent(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            // 1. Decode HTML character entities (e.g., &amp; -> &)
            var sanitized = WebUtility.HtmlDecode(text);

            // 2. Remove standard hyperlinks
            sanitized = Regex.Replace(sanitized, @"https?:\/\/[^\s/$.?#].[^\s]*", "", RegexOptions.IgnoreCase);

            // 3. Remove markdown style links e.g., [link text](url)
            sanitized = Regex.Replace(sanitized, @"\[(.*?)\]\(.*?\)", "$1", RegexOptions.IgnoreCase);

            // 4. Remove emojis. This regex covers a wide range of common emojis.
            sanitized = Regex.Replace(sanitized, @"[\uD800-\uDBFF][\uDC00-\uDFFF]|\uD83C[\uDC00-\uDFFF]|\uD83D[\uDC00-\uDFFF]|[\u2600-\u26FF]|[\u2700-\u27BF]", "");

            // 5. Replace multiple whitespace characters with a single space and trim
            sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();

            return sanitized;
        }

        /// <summary>
        /// Sanitizes a string to be a valid YouTube video title by decoding HTML, removing forbidden
        /// characters ('<' and '>'), and truncating it to 100 characters.
        /// </summary>
        /// <param name="title">The original title.</param>
        /// <returns>A sanitized title safe for YouTube upload.</returns>
        public static string SanitizeYouTubeTitle(string title)
        {
            // --- START OF MODIFICATION ---

            // 1. Decode HTML entities first to ensure consistency with API responses.
            var decodedTitle = WebUtility.HtmlDecode(title);

            // 2. Remove forbidden characters
            var sanitizedTitle = decodedTitle.Replace('<', ' ').Replace('>', ' ');

            // 3. Truncate to YouTube's 100-character limit
            if (sanitizedTitle.Length > 100)
            {
                sanitizedTitle = sanitizedTitle.Substring(0, 100);
            }

            // --- END OF MODIFICATION ---

            return sanitizedTitle;
        }

        /// <summary>
        /// Splits a long string of text into smaller pages, without breaking words.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <param name="maxCharactersPerPage">The maximum characters allowed on a page.</param>
        /// <returns>A list of pages.</returns>
        public static List<string> SplitTextIntoPages(string text, int maxCharactersPerPage)
        {
            var pages = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return pages;
            }

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var currentPage = new StringBuilder();

            foreach (var word in words)
            {
                if (currentPage.Length > 0 && currentPage.Length + word.Length + 1 > maxCharactersPerPage)
                {
                    pages.Add(currentPage.ToString().TrimEnd());
                    currentPage.Clear();
                }
                currentPage.Append(word + " ");
            }

            if (currentPage.Length > 0)
            {
                pages.Add(currentPage.ToString().TrimEnd());
            }

            return pages;
        }
    }
}