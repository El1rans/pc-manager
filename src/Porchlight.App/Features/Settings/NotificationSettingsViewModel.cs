using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.App.Shell;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Settings;

/// <summary>The Settings > Notifications page: which alerts to show and how often to check for app
/// updates. Every change is written straight to <see cref="ISettingsStore.Update"/> (no Save button).</summary>
public sealed partial class NotificationSettingsViewModel : PageViewModelBase
{
    private readonly ISettingsStore _settingsStore;

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

    public NotificationSettingsViewModel(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        var settings = settingsStore.Current.Notifications;

        // Assigned to the fields, not the properties: loading must not write settings back.
        _alertLowDisk = settings.AlertLowDisk;
        _alertTemperature = settings.AlertTemperature;
        _alertUpdates = settings.AlertUpdates;
        _alertRestartPending = settings.AlertRestartPending;
        _selectedSchedule = ScheduleOptions.FirstOrDefault(o => o.Value == settings.UpdateCheckSchedule) ?? ScheduleOptions[0];
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

    partial void OnAlertLowDiskChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertLowDisk = value);

    partial void OnAlertTemperatureChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertTemperature = value);

    partial void OnAlertUpdatesChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertUpdates = value);

    partial void OnAlertRestartPendingChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertRestartPending = value);

    partial void OnSelectedScheduleChanged(ScheduleOption value) =>
        _settingsStore.Update(s => s.Notifications.UpdateCheckSchedule = value.Value);
}
