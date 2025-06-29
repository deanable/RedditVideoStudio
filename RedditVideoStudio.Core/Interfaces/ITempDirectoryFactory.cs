namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines a factory for creating instances of ITempDirectory.
    /// This pattern allows for the creation of disposable resources
    /// without tightly coupling the application layer to the infrastructure layer.
    /// </summary>
    public interface ITempDirectoryFactory
    {
        ITempDirectory Create();
    }
}
