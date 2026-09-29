namespace Porchlight.Core.Cleanup;

/// <summary>The values of one uninstall registry key, before filtering. Kept separate from
/// <see cref="InstalledApp"/> so <see cref="InstalledAppFilter"/> is pure and unit-testable.</summary>
public sealed record RawUninstallEntry(
    string? DisplayName,
    string? Publisher,
    string? DisplayVersion,
    int? EstimatedSizeKb,
    string? InstallDate,
    string? UninstallString,
    int? SystemComponent,
    string? ParentKeyName,
    string? ReleaseType,
    bool IsPerMachine);
