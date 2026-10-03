using Porchlight.Core.Cleanup;

namespace Porchlight.Core.RemoveApps;

/// <summary>An installed program as the "Remove apps" page shows it.</summary>
/// <param name="App">The registry entry (name, publisher, size, date, uninstall command).</param>
/// <param name="Kind">How it is treated; see <see cref="RemovableAppKind"/>.</param>
/// <param name="IsOftenPreinstalled">True for programs that commonly come with a new PC.</param>
/// <param name="WingetId">The exact winget id when winget knows this program; otherwise null.</param>
public sealed record RemovableApp(
    InstalledApp App,
    RemovableAppKind Kind,
    bool IsOftenPreinstalled,
    string? WingetId)
{
    /// <summary>True when Porchlight may offer Remove for this program.</summary>
    public bool CanRemove => Kind is RemovableAppKind.Normal or RemovableAppKind.SystemPart;
}
