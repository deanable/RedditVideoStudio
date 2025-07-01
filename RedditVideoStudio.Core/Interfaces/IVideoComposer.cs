using RedditVideoStudio.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Core.Interfaces
{
    public interface IVideoComposer
    {
        Task ComposeVideoAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken);
        Task ComposeVideoAsync(string title, List<string> comments, IProgress<ProgressReport> progress, CancellationToken cancellationToken, string outputPath, string orientation);
    }
}