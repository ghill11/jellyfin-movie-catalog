using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieCatalog;

/// <summary>
/// Timer-reset debouncer with a "dirty during push" flag.
///
/// Contract:
///   Bump() restarts the quiet-window timer. When the window elapses
///   undisturbed, the build+push fires on a background Task. If a new
///   Bump arrives while a push is in flight, the call sets a dirty flag
///   instead of starting a parallel push; when the in-flight push
///   completes, the debouncer re-arms the quiet window for the next push.
///
/// Thread-safety: every state mutation is under _gate. Never await inside
/// the lock. _runningPush is the "currently executing" flag;
/// _dirtyDuringPush collapses N events arriving during a push into exactly
/// one follow-up fire.
/// </summary>
public class Debouncer
{
    private readonly object _gate = new();
    private CancellationTokenSource? _wait;
    private Task? _runningPush;
    private bool _dirtyDuringPush;
    private readonly MovieCatalogBuilder _builder;
    private readonly GitHubPusher _pusher;
    private readonly ILogger _logger;

    public Debouncer(MovieCatalogBuilder builder, GitHubPusher pusher, ILoggerFactory loggerFactory)
    {
        _builder = builder;
        _pusher = pusher;
        _logger = loggerFactory.CreateLogger("JellyfinMovieCatalog");
    }

    public void Bump()
    {
        var quietSeconds = Plugin.Instance?.Configuration?.DebounceSeconds ?? 30;
        if (quietSeconds < 1)
        {
            quietSeconds = 1;
        }

        CancellationToken token;
        lock (_gate)
        {
            if (_runningPush != null)
            {
                _dirtyDuringPush = true;
                _logger.LogDebug("Debouncer.Bump: in-flight push; marking dirty");
                return;
            }
            _wait?.Cancel();
            _wait = new CancellationTokenSource();
            token = _wait.Token;
        }

        _logger.LogDebug("Debouncer.Bump: quiet window armed for {Seconds}s", quietSeconds);
        _ = WaitThenFire(token, TimeSpan.FromSeconds(quietSeconds));
    }

    private async Task WaitThenFire(CancellationToken ct, TimeSpan quietWindow)
    {
        try
        {
            await Task.Delay(quietWindow, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        await FireAsync().ConfigureAwait(false);
    }

    private async Task FireAsync()
    {
        Task me;
        lock (_gate)
        {
            if (_runningPush != null)
            {
                _dirtyDuringPush = true;
                return;
            }
            me = _runningPush = Task.Run(DoBuildAndPushAsync);
        }
        try
        {
            await me.ConfigureAwait(false);
        }
        finally
        {
            bool requeue;
            lock (_gate)
            {
                _runningPush = null;
                requeue = _dirtyDuringPush;
                _dirtyDuringPush = false;
            }
            if (requeue)
            {
                _logger.LogDebug("Debouncer: dirty-during-push; re-arming");
                Bump();
            }
        }
    }

    public Task FlushAsync()
    {
        lock (_gate)
        {
            _wait?.Cancel();
        }
        return _runningPush ?? FireAsync();
    }

    private async Task DoBuildAndPushAsync()
    {
        try
        {
            var payload = await _builder.BuildAsync(CancellationToken.None).ConfigureAwait(false);
            await _pusher.PushAsync(payload, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debouncer build+push failed");
        }
    }
}
