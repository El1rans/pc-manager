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
}

/// <summary>An uninstall registry entry matched by <see cref="IRegistryReader.FindUninstallEntry"/>.</summary>
/// <param name="DisplayVersion">The installed version, if the entry has one.</param>
/// <param name="InstallLocation">The install directory, if the entry has one.</param>
public sealed record UninstallEntry(string? DisplayVersion, string? InstallLocation);
