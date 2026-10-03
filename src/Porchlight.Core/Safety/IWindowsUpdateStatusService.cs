namespace Porchlight.Core.Safety;

/// <summary>When did Windows last install updates, and are updates failing?</summary>
public interface IWindowsUpdateStatusService
{
    /// <summary>Quick status from the update history and restart flag. Never searches for updates.</summary>
    Task<WindowsUpdateStatus> GetAsync(CancellationToken cancellationToken);

    /// <summary>Explicitly searches for waiting updates; gives up after
    /// <see cref="SafetyTimeouts.PendingUpdateSearch"/>.</summary>
    Task<PendingUpdatesCheck> CheckPendingAsync(CancellationToken cancellationToken);
}
