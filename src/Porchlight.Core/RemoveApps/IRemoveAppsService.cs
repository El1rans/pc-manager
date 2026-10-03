namespace Porchlight.Core.RemoveApps;

/// <summary>Lists installed programs for the "Remove apps" page and removes one at a time.</summary>
public interface IRemoveAppsService
{
    /// <summary>Raised once after winget confirmed a removal. Seam for a future "Recent changes"
    /// journal; nothing in this feature depends on a subscriber.</summary>
    event EventHandler<AppRemovedEventArgs>? AppRemoved;

    /// <summary>Reads the installed programs (registry and winget) off the calling thread. Porchlight
    /// itself is never included.</summary>
    Task<IReadOnlyList<RemovableApp>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Removes <paramref name="app"/>: with winget when its id is known, otherwise through
    /// the app's own uninstaller. Refuses anything that is not removable or not in the last list.
    /// Never throws for an uninstall that simply fails.</summary>
    Task<RemoveAppOutcome> RemoveAsync(RemovableApp app, CancellationToken cancellationToken);
}
