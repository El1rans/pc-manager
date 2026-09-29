using System.IO.Enumeration;
using Porchlight.Core.Components;

namespace Porchlight.Core.Cleanup;

/// <summary>The per-root and per-file rules the scanner and the runner must agree on, so what the
/// scan reports is exactly what the clean would remove.</summary>
internal static class CleanupRules
{
    /// <summary>Roots that may be worked on right now: not admin-only while not elevated, and not
    /// belonging to a program that is currently running (safety rule 7). The friendly names of the
    /// blocking programs are returned so the UI can say "Close Chrome to clean its cache".</summary>
    public static List<CleanupRoot> SelectRoots(
        CleanupCategory category, bool isElevated, IProcessProbe processProbe, out List<string> blockedPrograms)
    {
        blockedPrograms = [];
        var roots = new List<CleanupRoot>();
        foreach (var root in category.Roots)
        {
            if (root.RequiresAdmin && !isElevated)
            {
                continue;
            }

            if (root.BlockingProcessName is { } processName && processProbe.IsRunning(processName))
            {
                var name = root.BlockingProcessDisplayName ?? processName;
                if (!blockedPrograms.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    blockedPrograms.Add(name);
                }

                continue;
            }

            roots.Add(root);
        }

        return roots;
    }

    /// <summary>True if <paramref name="name"/> matches the category's file-name pattern (or the
    /// category has none).</summary>
    public static bool MatchesPattern(CleanupCategory category, string name) =>
        category.FileNamePattern is null ||
        FileSystemName.MatchesSimpleExpression(category.FileNamePattern, name, ignoreCase: true);

    /// <summary>True if the entry is old enough to touch (safety rule 4).</summary>
    public static bool IsOldEnough(CleanupEntry entry, DateTime cutoffUtc) => entry.LastWriteUtc <= cutoffUtc;

    public static DateTime CutoffUtc(CleanupCategory category, TimeProvider timeProvider) =>
        timeProvider.GetUtcNow().UtcDateTime - category.MinimumAge;
}
