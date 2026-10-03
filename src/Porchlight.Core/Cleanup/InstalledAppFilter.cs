using Porchlight.Core.Components;

namespace Porchlight.Core.Cleanup;

/// <summary>Turns raw uninstall registry entries into the list of apps offered for uninstall,
/// leaving out anything that is not a normal, user-removable program.</summary>
public static class InstalledAppFilter
{
    private const int KilobytesToBytes = 1024;

    private static readonly string[] ExcludedReleaseTypes = ["Update", "Hotfix", "Security Update"];

    /// <summary>Names of Porchlight itself and of the components it depends on (removing them here
    /// would silently break a page; they have their own install cards).</summary>
    private static readonly string[] ProtectedNameFragments =
    [
        "Porchlight",
        .. ComponentCatalog.All.Select(component => component.UninstallDisplayNameMatch),
    ];

    /// <summary>Filters, de-duplicates (by display name + version, keeping the first, so per-machine
    /// entries listed first win) and sorts by size, largest first, then name. Apps with no reported
    /// size come last.</summary>
    /// <param name="entries">Raw uninstall entries.</param>
    /// <param name="includeProtectedNames">Keep Porchlight and its components in the result (the
    /// "Remove apps" page labels them instead of hiding them).</param>
    public static IReadOnlyList<InstalledApp> Apply(IEnumerable<RawUninstallEntry> entries, bool includeProtectedNames = false)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var apps = new List<InstalledApp>();
        foreach (var entry in entries)
        {
            if (!IsUserRemovable(entry, includeProtectedNames))
            {
                continue;
            }

            var name = entry.DisplayName!.Trim();
            if (!seen.Add($"{name}\u0001{entry.DisplayVersion?.Trim()}"))
            {
                continue;
            }

            apps.Add(new InstalledApp(
                name,
                NullIfBlank(entry.Publisher),
                NullIfBlank(entry.DisplayVersion),
                entry.EstimatedSizeKb is > 0 ? (long)entry.EstimatedSizeKb.Value * KilobytesToBytes : null,
                InstallDateParser.Parse(entry.InstallDate),
                entry.UninstallString!.Trim(),
                entry.IsPerMachine));
        }

        return apps
            .OrderByDescending(app => app.EstimatedSizeBytes ?? -1)
            .ThenBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsUserRemovable(RawUninstallEntry entry, bool includeProtectedNames)
    {
        if (string.IsNullOrWhiteSpace(entry.DisplayName) || string.IsNullOrWhiteSpace(entry.UninstallString))
        {
            return false;
        }

        if (entry.SystemComponent == 1 || !string.IsNullOrWhiteSpace(entry.ParentKeyName))
        {
            return false;
        }

        if (entry.ReleaseType is { } releaseType &&
            ExcludedReleaseTypes.Contains(releaseType.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return includeProtectedNames || !ProtectedNameFragments.Any(fragment =>
            entry.DisplayName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
