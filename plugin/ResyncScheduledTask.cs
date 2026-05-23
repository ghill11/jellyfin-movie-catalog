using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieCatalog;

/// <summary>
/// Manually-triggerable scheduled task that builds and pushes the catalog
/// immediately. No recurring trigger (empty Triggers list) so it never
/// runs on a schedule, only when the user clicks "Run" in the Jellyfin
/// admin's Scheduled Tasks page or POSTs to /ScheduledTasks/Running/{key}.
/// </summary>
public class ResyncScheduledTask : IScheduledTask
{
    private readonly MovieCatalogBuilder _builder;
    private readonly GitHubPusher _pusher;
    private readonly ILogger _logger;

    public ResyncScheduledTask(MovieCatalogBuilder builder, GitHubPusher pusher, ILoggerFactory loggerFactory)
    {
        _builder = builder;
        _pusher = pusher;
        _logger = loggerFactory.CreateLogger("JellyfinMovieCatalog");
    }

    /// <inheritdoc />
    public string Name => "Resync Movie Catalog Now";

    /// <inheritdoc />
    public string Description => "Builds the full movie catalog payload and pushes it to GitHub immediately.";

    /// <inheritdoc />
    public string Category => "Movie Catalog";

    /// <inheritdoc />
    public string Key => "MovieCatalogResync";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual resync triggered");
        progress.Report(0);
        var payload = await _builder.BuildAsync(cancellationToken).ConfigureAwait(false);
        progress.Report(50);
        await _pusher.PushAsync(payload, cancellationToken).ConfigureAwait(false);
        progress.Report(100);
        _logger.LogInformation("Manual resync complete");
    }
}
