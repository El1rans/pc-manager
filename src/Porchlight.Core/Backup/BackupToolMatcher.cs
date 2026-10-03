namespace Porchlight.Core.Backup;

/// <summary>Recognises well-known backup programs by their installed-app display name.</summary>
public static class BackupToolMatcher
{
    private static readonly (string Match, string FriendlyName)[] KnownTools =
    [
        ("Macrium Reflect", "Macrium Reflect"),
        ("Acronis", "Acronis"),
        ("Veeam Agent", "Veeam Agent"),
        ("Backblaze", "Backblaze"),
        ("Google Drive", "Google Drive"),
        ("Dropbox", "Dropbox"),
        ("iDrive", "IDrive"),
        ("Carbonite", "Carbonite"),
        ("EaseUS Todo Backup", "EaseUS Todo Backup"),
        ("Windows Backup", "Windows Backup"),
    ];

    /// <summary>Friendly names of known tools present in <paramref name="displayNames"/>, sorted and distinct.</summary>
    public static IReadOnlyList<string> Find(IEnumerable<string> displayNames)
    {
        ArgumentNullException.ThrowIfNull(displayNames);
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in displayNames)
        {
            foreach (var (match, friendly) in KnownTools)
            {
                if (name.Contains(match, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(friendly);
                }
            }
        }

        return [.. found];
    }
}
