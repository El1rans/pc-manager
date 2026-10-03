namespace Porchlight.Core.Safety;

/// <summary>What was found on the PC, before matching against the catalog.</summary>
/// <param name="ProcessNames">Running process names (with or without ".exe").</param>
/// <param name="InstalledAppNames">Display names from the uninstall list.</param>
/// <param name="ServiceNames">Windows service names and display names.</param>
public sealed record RemoteToolEvidence(
    IReadOnlyList<string> ProcessNames,
    IReadOnlyList<string> InstalledAppNames,
    IReadOnlyList<string> ServiceNames);
