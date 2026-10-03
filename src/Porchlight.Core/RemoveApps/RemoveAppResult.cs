namespace Porchlight.Core.RemoveApps;

/// <summary>What happened when a removal was requested.</summary>
public enum RemoveAppResult
{
    /// <summary>winget removed it.</summary>
    Removed,

    /// <summary>The app's own uninstaller was opened; Porchlight cannot tell when (or whether) it finishes.</summary>
    UninstallerOpened,

    /// <summary>Not allowed: Porchlight, a managed component, or an app that is not in the last list.</summary>
    Refused,

    /// <summary>Porchlight is running as administrator and this is a per-user app.</summary>
    BlockedWhileElevated,

    /// <summary>The app's uninstall command could not be understood.</summary>
    InvalidCommand,

    /// <summary>winget ran but did not remove it; see <see cref="RemoveAppOutcome.WingetOutcome"/>.</summary>
    WingetProblem,

    /// <summary>Windows could not start the uninstaller (or the permission prompt was declined).</summary>
    Failed,
}
