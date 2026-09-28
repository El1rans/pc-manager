namespace Porchlight.Core.Winget;

/// <summary>
/// Pure list operations over <c>Settings.UpdatesSettings.LastOutcomes</c> - see
/// <see cref="PersistedUpdateOutcome"/>. Kept free of <c>ISettingsStore</c> so it is trivially unit
/// testable; callers wrap each mutating call in <c>ISettingsStore.Update</c> themselves.
/// </summary>
public static class UpdateOutcomeMemory
{
    /// <summary>
    /// Hard cap on how many entries <see cref="Remember"/> keeps, oldest dropped first, regardless
    /// of pruning - a safety net against unbounded growth if <see cref="Prune"/> is ever skipped
    /// (e.g. a refresh that fails before it gets that far). In normal operation <see cref="Prune"/>
    /// keeps the list down to roughly the number of packages winget currently lists.
    /// </summary>
    public const int MaxEntries = 200;

    /// <summary>Finds the remembered outcome for <paramref name="packageId"/>, but only if it was
    /// recorded against the same <paramref name="availableVersion"/> winget currently reports -
    /// see <see cref="PersistedUpdateOutcome.AvailableVersion"/>. Returns null otherwise (including
    /// when the available version has since changed, per
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>: "Clear the entry ... when the available
    /// version changes").</summary>
    public static PersistedUpdateOutcome? Find(
        IEnumerable<PersistedUpdateOutcome> entries, string packageId, string availableVersion)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(availableVersion);

        foreach (var entry in entries)
        {
            if (string.Equals(entry.PackageId, packageId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.AvailableVersion, availableVersion, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Upserts <paramref name="outcome"/>, replacing any existing entry for the same
    /// package id (regardless of its remembered version - a package only ever has one remembered
    /// outcome at a time) and trimming down to <see cref="MaxEntries"/> if needed.</summary>
    public static void Remember(List<PersistedUpdateOutcome> entries, PersistedUpdateOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(outcome);

        entries.RemoveAll(e => string.Equals(e.PackageId, outcome.PackageId, StringComparison.OrdinalIgnoreCase));
        entries.Add(outcome);

        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(0, entries.Count - MaxEntries);
        }
    }

    /// <summary>Removes any remembered outcome for <paramref name="packageId"/> - called on a
    /// successful update/reinstall, per <c>docs/specs/09-friendly-update-outcomes.md</c>.</summary>
    public static void Forget(List<PersistedUpdateOutcome> entries, string packageId)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(packageId);

        entries.RemoveAll(e => string.Equals(e.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Drops every entry whose package id is not in <paramref name="currentPackageIds"/> -
    /// called after each refresh so an outcome for a package that is no longer listed (updated some
    /// other way, uninstalled entirely, or simply no longer has an upgrade available) does not sit
    /// around forever. Keeps the list bounded without relying solely on <see cref="MaxEntries"/>.</summary>
    public static void Prune(List<PersistedUpdateOutcome> entries, IReadOnlyCollection<string> currentPackageIds)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(currentPackageIds);

        var current = new HashSet<string>(currentPackageIds, StringComparer.OrdinalIgnoreCase);
        entries.RemoveAll(e => !current.Contains(e.PackageId));
    }
}
