namespace Porchlight.Core.Winget;

/// <summary>What Porchlight did for one <see cref="UpdateHistoryEntry"/>.</summary>
public enum UpdateHistoryAction
{
    /// <summary>A plain <c>winget upgrade</c>.</summary>
    Update,

    /// <summary>Uninstall then install the newest version.</summary>
    Reinstall,

    /// <summary>An install-only retry after a reinstall removed the old version.</summary>
    Install,
}
