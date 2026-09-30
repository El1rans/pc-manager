using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.App.Shell;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Notifications;

/// <summary>View model for the "Notifications" dialog. Every change is written straight to
/// <see cref="NotificationSettings"/> through <see cref="ISettingsStore.Update"/> (no Save button).</summary>
public sealed partial class NotificationsViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService? _themeService;

    [ObservableProperty]
    private bool _keepRunningInTray;

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
    private ThemeOption _selectedTheme;

    public NotificationsViewModel(ISettingsStore settingsStore, IThemeService? themeService = null)
    {
        _settingsStore = settingsStore;
        _themeService = themeService;
        var settings = settingsStore.Current.Notifications;

        // Assigned to the fields, not the properties: loading must not write settings back.
        _keepRunningInTray = settings.KeepRunningInTray;
        _alertLowDisk = settings.AlertLowDisk;
        _alertTemperature = settings.AlertTemperature;
        _alertUpdates = settings.AlertUpdates;
        _alertRestartPending = settings.AlertRestartPending;
        _selectedSchedule = ScheduleOptions.FirstOrDefault(o => o.Value == settings.UpdateCheckSchedule) ?? ScheduleOptions[0];
        _selectedTheme = ThemeOptions.FirstOrDefault(o => o.Value == settingsStore.Current.Appearance.Theme) ?? ThemeOptions[0];
    }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(AppTheme.System, "Match Windows"),
        new(AppTheme.Light, "Light"),
        new(AppTheme.Dark, "Dark"),
    ];

    public IReadOnlyList<ScheduleOption> ScheduleOptions { get; } =
    [
        new(UpdateCheckSchedule.Daily, "Every day"),
        new(UpdateCheckSchedule.Weekly, "Every week"),
        new(UpdateCheckSchedule.Never, "Never"),
    ];

    partial void OnKeepRunningInTrayChanged(bool value) => _settingsStore.Update(s => s.Notifications.KeepRunningInTray = value);

    partial void OnAlertLowDiskChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertLowDisk = value);

    partial void OnAlertTemperatureChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertTemperature = value);

    partial void OnAlertUpdatesChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertUpdates = value);

    partial void OnAlertRestartPendingChanged(bool value) => _settingsStore.Update(s => s.Notifications.AlertRestartPending = value);

    partial void OnSelectedScheduleChanged(ScheduleOption value) =>
        _settingsStore.Update(s => s.Notifications.UpdateCheckSchedule = value.Value);

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        _settingsStore.Update(s => s.Appearance.Theme = value.Value);
        _themeService?.Apply(value.Value);
    }
}
