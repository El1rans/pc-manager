using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.App.Shell;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Settings;

/// <summary>The Settings > Notifications page: which alerts to show and how often to check for app
/// updates. Every change is written straight to <see cref="ISettingsStore.Update"/> (no Save button).</summary>
public sealed partial class NotificationSettingsViewModel : PageViewModelBase
{
    private readonly ISettingsStore _settingsStore;
    private readonly TimeProvider _timeProvider;

    [ObservableProperty]
    private bool _alertLowDisk;

    [ObservableProperty]
    private bool _alertTemperature;

    [ObservableProperty]
    private bool _alertUpdates;

    [ObservableProperty]
    private bool _alertRestartPending;

    [ObservableProperty]
    private ScheduleOption _selectedSchedule;

    [ObservableProperty]
    private bool _checkupReminderEnabled;

    [ObservableProperty]
    private CheckupFrequencyOption _selectedCheckupFrequency;

    [ObservableProperty]
    private ReminderDayOption _selectedCheckupDay;

    public NotificationSettingsViewModel(ISettingsStore settingsStore, TimeProvider? timeProvider = null)
    {
        _settingsStore = settingsStore;
        _timeProvider = timeProvider ?? TimeProvider.System;
        var settings = settingsStore.Current.Notifications;

        // Assigned to the fields, not the properties: loading must not write settings back.
        _alertLowDisk = settings.AlertLowDisk;
        _alertTemperature = settings.AlertTemperature;
        _alertUpdates = settings.AlertUpdates;
        _alertRestartPending = settings.AlertRestartPending;
        _selectedSchedule = ScheduleOptions.FirstOrDefault(o => o.Value == settings.UpdateCheckSchedule) ?? ScheduleOptions[0];

        var reminder = settingsStore.Current.CheckupReminder;
        _checkupReminderEnabled = reminder.Enabled;
        _selectedCheckupFrequency = CheckupFrequencyOptions.FirstOrDefault(o => o.Value == reminder.Frequency) ?? CheckupFrequencyOptions[0];
        _selectedCheckupDay = CheckupDayOptions.FirstOrDefault(o => o.Value == reminder.Day) ?? CheckupDayOptions[^1];
    }

    public override string Title => "Notifications";

    // Segoe Fluent Icons "Ringer".
    public override string Glyph => "";

    public override int Order => 1;

    public override PageCategory Category => PageCategory.Settings;

    public IReadOnlyList<ScheduleOption> ScheduleOptions { get; } =
    [
        new(UpdateCheckSchedule.Daily, "Every day"),
        new(UpdateCheckSchedule.Weekly, "Every week"),
        new(UpdateCheckSchedule.Never, "Never"),
    ];

    public IReadOnlyList<CheckupFrequencyOption> CheckupFrequencyOptions { get; } =
    [
        new(CheckupReminderFrequency.Weekly, "Every week"),
        new(CheckupReminderFrequency.EveryTwoWeeks, "Every 2 weeks"),
        new(CheckupReminderFrequency.Monthly, "Every month"),
    ];

    /// <summary>Monday first, as in most of the world; Sunday is the default pick.</summary>
    public IReadOnlyList<ReminderDayOption> CheckupDayOptions { get; } =
    [
        new(DayOfWeek.Monday, "Monday"),
        new(DayOfWeek.Tuesday, "Tuesday"),
        new(DayOfWeek.Wednesday, "Wednesday"),
        new(DayOfWeek.Thursday, "Thursday"),
        new(DayOfWeek.Friday, "Friday"),
        new(DayOfWeek.Saturday, "Saturday"),
        new(DayOfWeek.Sunday, "Sunday"),
    ];

    partial void OnCheckupReminderEnabledChanged(bool value)
    {
        var now = _timeProvider.GetUtcNow();
        _settingsStore.Update(s =>
        {
            s.CheckupReminder.Enabled = value;
            s.CheckupReminder.EnabledSinceUtc = value ? now : null;
        });
    }

    partial void OnSelectedCheckupFrequencyChanged(CheckupFrequencyOption value) =>
        _settingsStore.Update(s => s.CheckupReminder.Frequency = value.Value);

    partial void OnSelectedCheckupDayChanged(ReminderDayOption value) =>
        _settingsStore.Update(s => s.CheckupReminder.Day = value.Value);

    partial void OnAlertLowDiskChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertLowDisk = value);

    partial void OnAlertTemperatureChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertTemperature = value);

    partial void OnAlertUpdatesChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertUpdates = value);

    partial void OnAlertRestartPendingChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertRestartPending = value);

    partial void OnSelectedScheduleChanged(ScheduleOption value) =>
        _settingsStore.Update(s => s.Notifications.UpdateCheckSchedule = value.Value);
}
