namespace PCManager.Core.Winget;

/// <summary>One row of a <c>winget upgrade</c> table, as parsed by <see cref="WingetTableParser"/>.</summary>
/// <param name="Name">Display name, e.g. <c>Microsoft Visual Studio Code (User)</c>.</param>
/// <param name="Id">Package identifier, e.g. <c>Microsoft.VisualStudioCode</c>.</param>
/// <param name="InstalledVersion">Currently installed version, or the literal string <c>"Unknown"</c>
/// when winget could not determine it.</param>
/// <param name="AvailableVersion">Version available to upgrade to.</param>
/// <param name="Source">Package source, e.g. <c>winget</c>.</param>
/// <param name="RequiresExplicit">True when this row came from a table after the first one (winget
/// prints a second table headed "require explicit targeting for upgrade" for packages excluded from
/// a plain <c>winget upgrade --all</c>).</param>
public sealed record WingetPackage(
    string Name,
    string Id,
    string InstalledVersion,
    string AvailableVersion,
    string Source,
    bool RequiresExplicit);
