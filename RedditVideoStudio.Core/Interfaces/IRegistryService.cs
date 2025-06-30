namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines a contract for a service that interacts with a persistent key-value store,
    /// such as the Windows Registry, for application settings.
    /// This interface MUST be public so it can be accessed by other projects.
    /// </summary>
    public interface IRegistryService
    {
        /// <summary>
        /// Retrieves a value from the store by its key.
        /// </summary>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <returns>The stored value.</returns>
        string GetValue(string key);

        /// <summary>
        /// Stores or updates a key-value pair in the store.
        /// </summary>
        /// <param name="key">The key of the value to set.</param>
        /// <param name="value">The value to store.</param>
        void SetValue(string key, string value);
    }
}