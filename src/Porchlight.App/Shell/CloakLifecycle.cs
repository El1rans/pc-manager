namespace Porchlight.App.Shell;

/// <summary>
/// The testable core of <see cref="WhiteFlashGuard"/>'s cloak lifecycle: decides when it becomes
/// safe to reveal a cloaked window, and makes sure that happens exactly once. Two independent
/// signals race to uncloak:
/// <list type="bullet">
/// <item>WPF actually finishing its first render pass (<see cref="OnContentRendered"/>) - the
/// normal, happy-path signal.</item>
/// <item>A bounded fallback timer, started the moment this lifecycle is created, that fires
/// regardless (<see cref="ForceUncloak"/> internally). This is the safety net: without it, a
/// window that never reaches <c>ContentRendered</c> - an exception during its first layout/render
/// pass, the window being shown minimized, or any other path that skips a normal render - would
/// stay cloaked (invisible, but still present in the taskbar) forever. For non-technical users
/// that is worse than the white flash this class exists to prevent.</item>
/// </list>
/// Whichever signal arrives first wins; the other is then a no-op. <see cref="ForceUncloak"/> is
/// also used for events that should reveal the window immediately regardless of render state
/// (e.g. the window's state changing, closing, or an app-wide crash-recovery sweep) - see
/// <see cref="WhiteFlashGuard"/>.
///
/// Takes a <see cref="TimeProvider"/> (via <see cref="TimeProvider.CreateTimer"/>) rather than a
/// <c>DispatcherTimer</c> for two reasons: it is unit-testable with a fake time provider without a
/// live WPF <c>Dispatcher</c>, and - more importantly - it keeps working even if the UI thread's
/// message pump is itself the thing that is stuck, which is exactly the scenario this fallback
/// exists to cover. A <c>DispatcherTimer</c> would not fire in that case.
/// </summary>
public sealed class CloakLifecycle : IDisposable
{
    private readonly object _gate = new();
    private readonly Action _uncloak;
    private ITimer? _fallbackTimer;
    private bool _uncloaked;

    public CloakLifecycle(TimeProvider timeProvider, TimeSpan fallbackDelay, Action uncloak)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(uncloak);
        _uncloak = uncloak;

        // Started immediately, not on some later "did we render yet" check - this is the only
        // thing that guarantees an uncloak even if nothing else in the window's lifecycle ever
        // fires again.
        _fallbackTimer = timeProvider.CreateTimer(_ => Uncloak(), null, fallbackDelay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>The real "it's safe to reveal this" signal: WPF has presented a frame.</summary>
    public void OnContentRendered() => Uncloak();

    /// <summary>Any other event that should force an uncloak regardless of render state.</summary>
    public void ForceUncloak() => Uncloak();

    private void Uncloak()
    {
        lock (_gate)
        {
            if (_uncloaked)
            {
                // Idempotent: whichever of ContentRendered/fallback-timer/ForceUncloak got here
                // first already did the real work; every later caller is a no-op.
                return;
            }

            _uncloaked = true;
            _fallbackTimer?.Dispose();
            _fallbackTimer = null;
        }

        // Invoked outside the lock: the callback (WhiteFlashGuard's DWM call, marshalled to the UI
        // thread) must never run while holding this lock, to rule out any reentrancy deadlock.
        _uncloak();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _fallbackTimer?.Dispose();
            _fallbackTimer = null;
        }
    }
}
