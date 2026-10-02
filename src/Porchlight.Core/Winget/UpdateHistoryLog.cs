using System.Globalization;

namespace Porchlight.Core.Winget;

/// <summary>Pure helpers for the update history list: capping and grouping by day.</summary>
public static class UpdateHistoryLog
{
    /// <summary>The most entries kept; the oldest are dropped first.</summary>
    public const int MaxEntries = 500;

    public const string TodayLabel = "Today";

    public const string YesterdayLabel = "Yesterday";

    private const string OlderDayFormat = "dddd, d MMMM";

    /// <summary>Appends <paramref name="entry"/> (entries are stored oldest first) and drops the
    /// oldest entries beyond <see cref="MaxEntries"/>.</summary>
    public static void Append(List<UpdateHistoryEntry> list, UpdateHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(entry);

        list.Add(entry);
        var excess = list.Count - MaxEntries;
        if (excess > 0)
        {
            list.RemoveRange(0, excess);
        }
    }

    /// <summary>Groups <paramref name="entries"/> by local day in <paramref name="zone"/>, newest day
    /// first and newest entry first within a day. Labels are "Today", "Yesterday", then e.g.
    /// "Monday, 28 September" (in <paramref name="culture"/>; when null, the invariant culture - the app's UI
    /// is English-only, so day and month names must not follow the PC's regional format).</summary>
    public static IReadOnlyList<(string Label, IReadOnlyList<UpdateHistoryEntry> Entries)> GroupByDay(
        IEnumerable<UpdateHistoryEntry> entries, DateTimeOffset now, TimeZoneInfo zone, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(zone);

        culture ??= CultureInfo.InvariantCulture;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        return entries
            .Select(e => (Entry: e, Day: DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(e.TimestampUtc, zone).DateTime)))
            .OrderByDescending(x => x.Entry.TimestampUtc)
            .GroupBy(x => x.Day)
            .OrderByDescending(g => g.Key)
            .Select(g => (
                Label: LabelFor(g.Key, today, culture),
                Entries: (IReadOnlyList<UpdateHistoryEntry>)g.Select(x => x.Entry).ToList()))
            .ToList();
    }

    private static string LabelFor(DateOnly day, DateOnly today, CultureInfo culture)
    {
        if (day == today)
        {
            return TodayLabel;
        }

        return day == today.AddDays(-1)
            ? YesterdayLabel
            : day.ToString(OlderDayFormat, culture);
    }
}
