namespace Porchlight.Core.Cleanup;

/// <summary>An installed program the user might want to uninstall.</summary>
/// <param name="DisplayName">Name as shown in Windows' Installed apps.</param>
/// <param name="Publisher">Publisher, if the program registered one.</param>
/// <param name="Version">Version, if the program registered one.</param>
/// <param name="EstimatedSizeBytes">Size the program reported (registry <c>EstimatedSize</c> is in KB),
/// or null when it did not report one.</param>
/// <param name="InstallDate">When it was installed, if known.</param>
/// <param name="UninstallString">The program's own uninstall command line.</param>
/// <param name="IsPerMachine">True if registered under HKLM (admin-writable only); false if under
/// HKCU (writable by the current user) - see <see cref="IAppUninstaller"/>'s elevation rule.</param>
public sealed record InstalledApp(
    string DisplayName,
    string? Publisher,
    string? Version,
    long? EstimatedSizeBytes,
    DateOnly? InstallDate,
    string UninstallString,
    bool IsPerMachine);
