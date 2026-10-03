using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Checkup;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.Core.Tests.Checkup;

public sealed class CheckupReminderSchedulerTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    // 2026-10-04 is a Sunday.
    private static DateTimeOffset At(int month, int day, int hour = 12) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider Clock(DateTimeOffset now)
    {
        var time = new FakeTimeProvider(now);
        time.SetLocalTimeZone(Utc);
        return time;
    }

    private static CheckupReminderSettings On(
        DateTimeOffset enabledSince,
        CheckupReminderFrequency frequency = CheckupReminderFrequency.Weekly,
        DayOfWeek day = DayOfWeek.Sunday) =>
        new() { Enabled = true, EnabledSinceUtc = enabledSince, Frequency = frequency, Day = day };

    [Fact]
    public void Disabled_IsNeverDue_AndHasNoNextDate()
    {
        var scheduler = new CheckupReminderScheduler(Clock(At(10, 4)));
        var settings = new CheckupReminderSettings { Enabled = false };

        Assert.False(scheduler.IsDue(settings));
        Assert.Null(scheduler.NextReminderDate(settings));
    }

    [Fact]
    public void FirstReminder_IsTheChosenWeekdayOnOrAfterTheDayItWasTurnedOn()
    {
        // Turned on Wednesday 2026-09-30, Sunday is 2026-10-04.
        var settings = On(At(9, 30));

        var early = new CheckupReminderScheduler(Clock(At(10, 3)));
        Assert.False(early.IsDue(settings));
        Assert.Equal(new DateOnly(2026, 10, 4), early.NextReminderDate(settings));

        var onTheDay = new CheckupReminderScheduler(Clock(At(10, 4, 0)));
        Assert.True(onTheDay.IsDue(settings));
    }

    [Fact]
    public void TurnedOnOnTheChosenWeekday_IsDueToday()
    {
        var scheduler = new CheckupReminderScheduler(Clock(At(10, 4)));

        Assert.True(scheduler.IsDue(On(At(10, 4, 9))));
    }

    [Fact]
    public void ReminderShown_WaitsOneFullPeriod()
    {
        var settings = On(At(9, 1));
        settings.LastReminderShownUtc = At(10, 4);

        Assert.Equal(new DateOnly(2026, 10, 11), new CheckupReminderScheduler(Clock(At(10, 4))).NextReminderDate(settings));
        Assert.False(new CheckupReminderScheduler(Clock(At(10, 10))).IsDue(settings));
        Assert.True(new CheckupReminderScheduler(Clock(At(10, 11))).IsDue(settings));
    }

    [Fact]
    public void RecentReport_SkipsThePeriod()
    {
        // Report made Friday 2026-10-02; weekly: not before Fri+7 = 10-09, so first Sunday on/after is 10-11.
        var settings = On(At(9, 1));
        settings.LastReminderShownUtc = At(9, 27);
        settings.LastReportCreatedUtc = At(10, 2);

        var scheduler = new CheckupReminderScheduler(Clock(At(10, 4)));

        Assert.False(scheduler.IsDue(settings));
        Assert.Equal(new DateOnly(2026, 10, 11), scheduler.NextReminderDate(settings));
    }

    [Fact]
    public void OldReport_DoesNotBlockAReminder()
    {
        var settings = On(At(8, 1));
        settings.LastReportCreatedUtc = At(9, 1);

        Assert.True(new CheckupReminderScheduler(Clock(At(10, 4))).IsDue(settings));
    }

    [Theory]
    [InlineData(CheckupReminderFrequency.Weekly, 10, 11)]
    [InlineData(CheckupReminderFrequency.EveryTwoWeeks, 10, 18)]
    [InlineData(CheckupReminderFrequency.Monthly, 11, 8)]
    public void Frequency_SetsTheNextDate(CheckupReminderFrequency frequency, int month, int day)
    {
        // Last reminder Sunday 2026-10-04.
        var settings = On(At(9, 1), frequency);
        settings.LastReminderShownUtc = At(10, 4);

        Assert.Equal(new DateOnly(2026, month, day), new CheckupReminderScheduler(Clock(At(10, 4))).NextReminderDate(settings));
    }

    [Fact]
    public void MissedWhilePcWasOff_ShowsOnceThenWaitsAFullPeriod()
    {
        var settings = On(At(8, 1));
        settings.LastReminderShownUtc = At(8, 30);

        // Several periods later, on a Tuesday: due (once)...
        var time = Clock(At(10, 20));
        var scheduler = new CheckupReminderScheduler(time);
        Assert.True(scheduler.IsDue(settings));

        // ...and once shown, the next one is a normal period away, not a catch-up series.
        settings.LastReminderShownUtc = time.GetUtcNow();
        Assert.False(scheduler.IsDue(settings));
        Assert.Equal(new DateOnly(2026, 11, 1), scheduler.NextReminderDate(settings));
    }

    [Fact]
    public void ReEnabledAfterLongOff_DoesNotFireInThePast()
    {
        var settings = On(At(10, 7));   // turned on again Wednesday 10-07
        settings.LastReminderShownUtc = At(8, 2);

        var scheduler = new CheckupReminderScheduler(Clock(At(10, 7)));

        Assert.False(scheduler.IsDue(settings));
        Assert.Equal(new DateOnly(2026, 10, 11), scheduler.NextReminderDate(settings));
    }

    [Fact]
    public void LocalDate_IsUsedNotUtc()
    {
        // 2026-10-04 23:30 UTC is already Monday 10-05 in UTC+2; a Monday reminder is due.
        var plus2 = TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2");
        var time = new FakeTimeProvider(At(10, 4, 23));
        time.SetLocalTimeZone(plus2);
        var settings = On(At(9, 1), day: DayOfWeek.Monday);

        Assert.True(new CheckupReminderScheduler(time).IsDue(settings));
    }

    [Fact]
    public void DaylightSavingChange_DoesNotShiftTheDay()
    {
        // US fall-back is 2026-11-01 at 02:00 local. A reminder shown Sunday 10-25 20:00 local
        // (2026-10-26 00:00 UTC in Eastern daylight time) is due again Sunday 11-01, not Saturday.
        var eastern = TimeZoneInfo.CreateCustomTimeZone(
            "test-eastern", TimeSpan.FromHours(-5), "eastern", "EST", "EDT",
            [TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 8),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1))]);

        var settings = On(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        settings.LastReminderShownUtc = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);

        // Saturday 2026-10-31 23:30 local EDT = 2026-11-01 03:30 UTC: not yet due.
        var saturday = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 3, 30, 0, TimeSpan.Zero));
        saturday.SetLocalTimeZone(eastern);
        Assert.False(new CheckupReminderScheduler(saturday).IsDue(settings));

        // Sunday 2026-11-01 00:30 local EDT = 04:30 UTC: due.
        var sunday = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 4, 30, 0, TimeSpan.Zero));
        sunday.SetLocalTimeZone(eastern);
        var scheduler = new CheckupReminderScheduler(sunday);
        Assert.True(scheduler.IsDue(settings));
        Assert.Equal(new DateOnly(2026, 11, 1), scheduler.NextReminderDate(settings));
    }

    [Fact]
    public void MonthlyFromEndOfMonth_ClampsToShorterMonth()
    {
        var settings = On(At(1, 1), CheckupReminderFrequency.Monthly, DayOfWeek.Monday);
        settings.LastReminderShownUtc = At(1, 31);

        // 2026-01-31 + 1 month = 2026-02-28 (Saturday); next Monday is 03-02.
        Assert.Equal(new DateOnly(2026, 3, 2), new CheckupReminderScheduler(Clock(At(2, 1))).NextReminderDate(settings));
    }

    [Fact]
    public void LastReportDate_IsTheLocalDay()
    {
        var settings = On(At(9, 1));
        settings.LastReportCreatedUtc = At(10, 2);

        Assert.Equal(new DateOnly(2026, 10, 2), new CheckupReminderScheduler(Clock(At(10, 4))).LastReportDate(settings));
        Assert.Null(new CheckupReminderScheduler(Clock(At(10, 4))).LastReportDate(new CheckupReminderSettings()));
    }
}
