using Porchlight.Core.Settings;

namespace Porchlight.Core.Checkup;

/// <summary>
/// Pure scheduling rules for the check-up reminder. Everything works on local calendar dates (never
/// on added hours), so daylight-saving changes cannot move a reminder to the wrong day.
/// <para>
/// The period start is the later of the last report and the last reminder. The next reminder is due on
/// the first chosen weekday on or after period start + one period. If the PC was off for several
/// periods the reminder is simply overdue and fires once; showing it moves the period start to today.
/// With no report or reminder yet, the first reminder is the first chosen weekday on or after the day
/// the reminder was turned on.
/// </para>
/// </summary>
public sealed class CheckupReminderScheduler(TimeProvider timeProvider)
{
    private const int DaysPerWeek = 7;
    private const int WeeksInFortnight = 2;

    /// <summary>True when a reminder should be shown right now.</summary>
    public bool IsDue(CheckupReminderSettings settings)
    {
        var next = NextReminderDate(settings);
        return next is not null && Today() >= next.Value;
    }

    /// <summary>The day the next reminder is due, or null when reminders are off. An overdue reminder
    /// returns a date in the past.</summary>
    public DateOnly? NextReminderDate(CheckupReminderSettings settings)
    {
        if (!settings.Enabled)
        {
            return null;
        }

        var lastReport = ToLocalDate(settings.LastReportCreatedUtc);
        var lastReminder = ToLocalDate(settings.LastReminderShownUtc);
        var enabledSince = ToLocalDate(settings.EnabledSinceUtc) ?? Today();

        var periodStart = Latest(lastReport, lastReminder);
        if (periodStart is null)
        {
            return OnOrAfter(enabledSince, settings.Day);
        }

        var earliest = AddPeriod(periodStart.Value, settings.Frequency);

        // Turned on again after the last report or reminder: never schedule before that day.
        return OnOrAfter(Max(earliest, enabledSince), settings.Day);
    }

    /// <summary>The local day of the last report, for display.</summary>
    public DateOnly? LastReportDate(CheckupReminderSettings settings) => ToLocalDate(settings.LastReportCreatedUtc);

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private DateOnly? ToLocalDate(DateTimeOffset? utc) =>
        utc is null ? null : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc.Value, timeProvider.LocalTimeZone).DateTime);

    private static DateOnly? Latest(DateOnly? a, DateOnly? b) =>
        a is null ? b : b is null ? a : Max(a.Value, b.Value);

    private static DateOnly Max(DateOnly a, DateOnly b) => a >= b ? a : b;

    private static DateOnly AddPeriod(DateOnly from, CheckupReminderFrequency frequency) => frequency switch
    {
        CheckupReminderFrequency.EveryTwoWeeks => from.AddDays(DaysPerWeek * WeeksInFortnight),
        CheckupReminderFrequency.Monthly => from.AddMonths(1),
        _ => from.AddDays(DaysPerWeek),
    };

    private static DateOnly OnOrAfter(DateOnly date, DayOfWeek day)
    {
        var diff = ((int)day - (int)date.DayOfWeek + DaysPerWeek) % DaysPerWeek;
        return date.AddDays(diff);
    }
}
