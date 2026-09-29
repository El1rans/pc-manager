namespace Porchlight.Core.Cleanup;

/// <summary>Lists installed programs from the three uninstall registry hives (HKLM 64-bit, HKLM
/// WOW6432Node, HKCU), already filtered by <see cref="InstalledAppFilter"/>.</summary>
public interface IInstalledAppsReader
{
    /// <summary>Reads the registry; call off the UI thread.</summary>
    IReadOnlyList<InstalledApp> GetInstalledApps();
}
