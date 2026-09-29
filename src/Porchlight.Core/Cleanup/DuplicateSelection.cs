namespace Porchlight.Core.Cleanup;

/// <summary>Which copy of a duplicate group to keep.</summary>
public static class DuplicateSelection
{
    /// <summary>The copy to keep: the most recently modified one. Ties are broken by the shorter,
    /// then alphabetically first, path so the answer is always the same.</summary>
    public static DuplicateFile PickNewest(DuplicateGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.Files
            .OrderByDescending(file => file.LastWriteUtc)
            .ThenBy(file => file.FullPath.Length)
            .ThenBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    /// <summary>The paths to remove for "Keep newest": every copy except <see cref="PickNewest"/>.</summary>
    public static IReadOnlyList<string> SuggestKeepNewest(DuplicateGroup group)
    {
        var keep = PickNewest(group);
        return group.Files.Where(file => !ReferenceEquals(file, keep)).Select(file => file.FullPath).ToList();
    }
}
