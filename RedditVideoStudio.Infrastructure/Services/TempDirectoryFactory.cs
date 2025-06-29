using RedditVideoStudio.Core.Interfaces;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the ITempDirectoryFactory interface to provide instances
    /// of the TempDirectory class. This factory is essential for decoupling
    /// the creation of disposable resources from the application layer.
    /// </summary>
    public class TempDirectoryFactory : ITempDirectoryFactory
    {
        public ITempDirectory Create()
        {
            return new TempDirectory();
        }
    }
}