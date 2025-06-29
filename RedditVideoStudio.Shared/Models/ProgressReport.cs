namespace RedditVideoStudio.Shared.Models
{
    /// <summary>
    /// Represents a progress report for long-running operations,
    /// containing a percentage completion value and a descriptive message.
    /// </summary>
    public class ProgressReport
    {
        public int Percentage { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
