using Porchlight.Core.Cleanup;
using Porchlight.Core.Winget;

namespace Porchlight.Core.RemoveApps;

/// <summary>Finds the winget id of an installed program from <c>winget list</c> rows. Never guesses:
/// when the match is ambiguous it returns null and the app's own uninstaller is used instead.</summary>
public static class WingetIdMapper
{
    private const string Ellipsis = "…";
    private const string ThreeDots = "...";

    public static string? FindId(InstalledApp app, IReadOnlyList<WingetInstalledPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(packages);

        var candidates = packages.Where(p => NamesMatch(app.DisplayName, p.Name)).ToList();
        if (candidates.Count == 1)
        {
            return candidates[0].Id;
        }

        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(app.Version))
        {
            return null;
        }

        var sameVersion = candidates
            .Where(p => string.Equals(p.Version, app.Version, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return sameVersion.Count == 1 ? sameVersion[0].Id : null;
    }

    private static bool NamesMatch(string registryName, string wingetName)
    {
        var registry = registryName.Trim();
        var winget = wingetName.Trim();
        if (winget.Length == 0)
        {
            return false;
        }

        if (winget.EndsWith(Ellipsis, StringComparison.Ordinal) || winget.EndsWith(ThreeDots, StringComparison.Ordinal))
        {
            var prefix = winget.TrimEnd('.', '…').TrimEnd();
            return prefix.Length > 0 && registry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(registry, winget, StringComparison.OrdinalIgnoreCase);
    }
}
