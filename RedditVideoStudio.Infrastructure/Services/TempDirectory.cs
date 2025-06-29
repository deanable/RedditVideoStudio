using RedditVideoStudio.Core.Interfaces;
using System;
using System.IO;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the ITempDirectory interface to provide a self-cleaning temporary directory.
    /// The directory is created upon instantiation and is automatically deleted when the object is disposed.
    /// </summary>
    public class TempDirectory : ITempDirectory
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RedditVideoStudio", Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
            catch (Exception)
            {
                // Log this exception in a real-world scenario. For now, we'll ignore it
                // as the OS will eventually clean up the temp folder.
            }
            GC.SuppressFinalize(this);
        }
    }
}
