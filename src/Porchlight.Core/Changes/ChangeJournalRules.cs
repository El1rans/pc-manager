namespace Porchlight.Core.Changes;

/// <summary>Pure rules for how much of the journal is kept.</summary>
public static class ChangeJournalRules
{
    /// <summary>The most entries kept; the oldest are dropped first.</summary>
    public const int MaxEntries = 500;

    /// <summary>Entries older than this many days are dropped.</summary>
    public const int MaxAgeDays = 90;

    /// <summary>Drops entries older than <see cref="MaxAgeDays"/> and beyond <see cref="MaxEntries"/>.
    /// <paramref name="oldestFirst"/> is edited in place and stays oldest first.</summary>
    public static void Trim(List<ChangeEntry> oldestFirst, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(oldestFirst);

        var cutoff = now - TimeSpan.FromDays(MaxAgeDays);
        oldestFirst.RemoveAll(e => e.Time < cutoff);

        var excess = oldestFirst.Count - MaxEntries;
        if (excess > 0)
        {
            oldestFirst.RemoveRange(0, excess);
        }
    }
}
