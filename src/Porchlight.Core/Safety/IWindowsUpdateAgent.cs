namespace Porchlight.Core.Safety;

/// <summary>The only class that talks to the Windows Update Agent (COM) and the pending-restart
/// registry flag. Keeps COM out of the rest of the app and out of tests.</summary>
public interface IWindowsUpdateAgent
{
    /// <summary>Recent install history, newest first. Null when it cannot be read.</summary>
    Task<IReadOnlyList<UpdateHistoryEntry>?> ReadHistoryAsync(CancellationToken cancellationToken);

    /// <summary>True when Windows is waiting for a restart to finish updating; null when unknown.</summary>
    Task<bool?> IsRestartPendingAsync(CancellationToken cancellationToken);

    /// <summary>Searches for updates that are waiting. Slow (can take minutes): only on request.
    /// Returns null when the search failed. Not cancellable once started; the caller times out.</summary>
    Task<int?> CountPendingAsync(CancellationToken cancellationToken);
}
