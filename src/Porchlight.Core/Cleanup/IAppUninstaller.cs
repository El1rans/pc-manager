namespace Porchlight.Core.Cleanup;

/// <summary>Starts an installed program's own uninstaller. Never uses the quiet uninstall command or
/// adds silent flags: the program's own window runs and the user stays in control.</summary>
public interface IAppUninstaller
{
    /// <summary>False while Porchlight is elevated and <paramref name="app"/> is a per-user (HKCU)
    /// entry: a user-writable registry value must never be run with administrator rights.</summary>
    bool CanStartUninstall(InstalledApp app);

    UninstallStartResult StartUninstall(InstalledApp app);
}
