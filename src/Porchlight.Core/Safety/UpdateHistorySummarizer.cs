namespace Porchlight.Core.Safety;

/// <summary>Summarises the Windows Update history. Pure.</summary>
public static class UpdateHistorySummarizer
{
    /// <summary>How far back a failure still counts as "recent".</summary>
    public static readonly TimeSpan RecentWindow = TimeSpan.FromDays(30);

    /// <summary>Title fragments of the daily antivirus definition updates; they would make "last
    /// installed" look recent even when Windows itself is not being updated.</summary>
    private static readonly string[] IgnoredTitleFragments =
    [
        "Security Intelligence Update",
        "Definition Update",
        "Platform Update for Microsoft Defender",
    ];

    public static UpdateHistorySummary Summarize(IEnumerable<UpdateHistoryEntry> entries, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(entries);

        DateTimeOffset? lastInstalled = null;
        var failures = 0;
        foreach (var entry in entries)
        {
            if (IsDefinitionUpdate(entry.Title))
            {
                continue;
            }

            if (entry.Result is UpdateHistoryResult.Succeeded or UpdateHistoryResult.SucceededWithErrors)
            {
                if (lastInstalled is null || entry.Date > lastInstalled)
                {
                    lastInstalled = entry.Date;
                }
            }
            else if (entry.Result == UpdateHistoryResult.Failed && now - entry.Date <= RecentWindow)
            {
                failures++;
            }
        }

        return new UpdateHistorySummary(lastInstalled, failures);
    }

    private static bool IsDefinitionUpdate(string title) =>
        IgnoredTitleFragments.Any(f => title.Contains(f, StringComparison.OrdinalIgnoreCase));
}
