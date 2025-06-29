using RedditVideoStudio.Domain.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for a service that fetches post and comment data from the Reddit API.
    /// </summary>
    public interface IRedditService
    {
        /// <summary>
        /// Asynchronously fetches a list of top posts and their comments from a configured subreddit.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of RedditPostData objects, each containing post details and comments.</returns>
        Task<List<RedditPostData>> FetchFullPostDataAsync(CancellationToken cancellationToken);
    }
}
