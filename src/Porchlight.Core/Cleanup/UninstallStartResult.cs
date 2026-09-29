namespace Porchlight.Core.Cleanup;

/// <summary>What happened when an uninstall was requested.</summary>
public enum UninstallStartResult
{
    /// <summary>The program's own uninstaller was launched.</summary>
    Started,

    /// <summary>Not launched: Porchlight is elevated and this is a per-user entry, whose command a
    /// non-admin could have planted. The page offers "Open Installed apps" instead.</summary>
    BlockedWhileElevated,

    /// <summary>The uninstall command could not be understood.</summary>
    InvalidCommand,

    /// <summary>Windows could not start the uninstaller (or the user declined its admin prompt).</summary>
    Failed,
}
