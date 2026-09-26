namespace PCManager.Core.Components;

/// <summary>
/// The narrow slice of registry access <c>ComponentService</c> needs for detection, behind an
/// interface so detection is unit-testable without touching the real registry.
/// </summary>
public interface IRegistryReader
{
    /// <summary>
    /// Looks for an entry under either uninstall registry hive (HKLM 64-bit, HKLM 32-bit/WOW6432Node,
    /// and HKCU, matching how installers register per-machine or per-user) whose
    /// <c>DisplayName</c> contains <paramref name="displayNameContains"/> (case-insensitive).
    /// Returns the entry's <c>DisplayVersion</c> and <c>InstallLocation</c> if found.
    /// </summary>
    UninstallEntry? FindUninstallEntry(string displayNameContains);

    /// <summary>Whether a service with this exact name is registered
    /// (<c>HKLM\SYSTEM\CurrentControlSet\Services\&lt;name&gt;</c>), regardless of its run state.</summary>
    bool ServiceExists(string serviceName);

    /// <summary>
    /// Component ids the PC Manager installer (milestone 07) already set up on the user's behalf,
    /// read from <c>HKLM\Software\PC Manager\Installer</c>, value <c>Components</c> (a REG_SZ,
    /// comma-separated list of component ids - see docs/specs/07-installer.md, "Contract with
    /// first-run setup"). Empty if the value is absent (e.g. PC Manager was not installed by that
    /// installer, or it handled nothing). First-run setup still re-detects every component itself;
    /// this is only a hint for which ones to leave unticked by default.
    /// </summary>
    IReadOnlyList<string> GetInstallerHandledComponentIds();
}

/// <summary>An uninstall registry entry matched by <see cref="IRegistryReader.FindUninstallEntry"/>.</summary>
/// <param name="DisplayVersion">The installed version, if the entry has one.</param>
/// <param name="InstallLocation">The install directory, if the entry has one.</param>
/// <param name="IsPerMachine">
/// True if the entry was found under an HKLM uninstall key (a per-machine install, only writable
/// by an administrator); false if it came from HKCU (a per-user install, writable by the current
/// user). Used to decide whether a resolved exe path is safe to launch while running elevated -
/// see <c>ComponentService</c>'s use of <see cref="PCManager.Core.Elevation.IElevationService"/>.
/// </param>
public sealed record UninstallEntry(string? DisplayVersion, string? InstallLocation, bool IsPerMachine = true);
