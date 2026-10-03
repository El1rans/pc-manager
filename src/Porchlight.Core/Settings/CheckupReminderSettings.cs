namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the check-up reminder. See <c>docs/specs/38-checkup-reminder.md</c>.</summary>
public sealed class CheckupReminderSettings
{
    /// <summary>"Remind me to send a check-up report". Off by default.</summary>
    public bool Enabled { get; set; }

    public CheckupReminderFrequency Frequency { get; set; } = CheckupReminderFrequency.Weekly;

    /// <summary>The weekday the reminder is due on (local time).</summary>
    public DayOfWeek Day { get; set; } = DayOfWeek.Sunday;

    /// <summary>When the reminder was turned on; the first reminder is the first chosen weekday on or
    /// after this day. Null while off.</summary>
    public DateTimeOffset? EnabledSinceUtc { get; set; }

    /// <summary>When the reminder balloon was last shown.</summary>
    public DateTimeOffset? LastReminderShownUtc { get; set; }

    /// <summary>When the person last made a check-up report (created, copied, saved or emailed it).</summary>
    public DateTimeOffset? LastReportCreatedUtc { get; set; }
}
