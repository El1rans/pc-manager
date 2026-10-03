namespace Porchlight.App.Features.Settings;

/// <summary>One choice in the check-up reminder weekday picker.</summary>
public sealed record ReminderDayOption(DayOfWeek Value, string Label);
