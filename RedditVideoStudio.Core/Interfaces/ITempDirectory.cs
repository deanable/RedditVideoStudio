using System;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a disposable temporary directory.
    /// This ensures that any created temporary directories are automatically
    /// cleaned up when they are no longer needed.
    /// </summary>
    public interface ITempDirectory : IDisposable
    {
        /// <summary>
        /// Gets the full path of the temporary directory.
        /// </summary>
        string Path { get; }
    }
}
