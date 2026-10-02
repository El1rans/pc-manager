using System.Globalization;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class UpdateHistoryLogTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("UTC+10", TimeSpan.FromHours(10), "UTC+10", "UTC+10");
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static UpdateHistoryEntry Entry(DateTimeOffset utc, string id = "A.Id") =>
        new() { TimestampUtc = utc, PackageId = id, PackageName = id };

    [Fact]
    public void Append_KeepsAtMostMaxEntries_DroppingTheOldest()
    {
        var list = new List<UpdateHistoryEntry>();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < UpdateHistoryLog.MaxEntries + 3; i++)
        {
            UpdateHistoryLog.Append(list, Entry(start.AddMinutes(i), "Pkg" + i.ToString(CultureInfo.InvariantCulture)));
        }

        Assert.Equal(UpdateHistoryLog.MaxEntries, list.Count);
        Assert.Equal("Pkg3", list[0].PackageId);
        Assert.Equal("Pkg" + (UpdateHistoryLog.MaxEntries + 2).ToString(CultureInfo.InvariantCulture), list[^1].PackageId);
    }

    [Fact]
    public void GroupByDay_LabelsTodayYesterdayAndOlderDates_NewestFirst()
    {
        // "Now" is 2026-09-30 00:30 local (UTC+10) = 2026-09-29 14:30 UTC.
        var now = new DateTimeOffset(2026, 9, 29, 14, 30, 0, TimeSpan.Zero);
        var justAfterMidnight = Entry(new DateTimeOffset(2026, 9, 29, 14, 10, 0, TimeSpan.Zero), "Today1");
        // 23:50 local on the 29th = 13:50 UTC on the 29th: yesterday in local time, even though "today" in UTC.
        var justBeforeMidnight = Entry(new DateTimeOffset(2026, 9, 29, 13, 50, 0, TimeSpan.Zero), "Yesterday1");
        var older = Entry(new DateTimeOffset(2026, 9, 27, 20, 0, 0, TimeSpan.Zero), "Older1"); // 28 Sep 06:00 local

        var groups = UpdateHistoryLog.GroupByDay([older, justBeforeMidnight, justAfterMidnight], now, Zone, Culture);

        Assert.Equal(["Today", "Yesterday", "Monday, 28 September"], groups.Select(g => g.Label));
        Assert.Equal("Today1", Assert.Single(groups[0].Entries).PackageId);
        Assert.Equal("Yesterday1", Assert.Single(groups[1].Entries).PackageId);
        Assert.Equal("Older1", Assert.Single(groups[2].Entries).PackageId);
    }

    [Fact]
    public void GroupByDay_OrdersEntriesNewestFirstWithinADay()
    {
        var now = new DateTimeOffset(2026, 9, 29, 14, 30, 0, TimeSpan.Zero);
        var early = Entry(new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.Zero), "Early");
        var late = Entry(new DateTimeOffset(2026, 9, 29, 5, 0, 0, TimeSpan.Zero), "Late");

        var group = Assert.Single(UpdateHistoryLog.GroupByDay([early, late], now, Zone, Culture));

        Assert.Equal(["Late", "Early"], group.Entries.Select(e => e.PackageId));
    }

    [Fact]
    public void GroupByDay_NoEntries_ReturnsNoGroups()
    {
        Assert.Empty(UpdateHistoryLog.GroupByDay([], DateTimeOffset.UtcNow, Zone, Culture));
    }
}
