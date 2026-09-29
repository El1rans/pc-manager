using Porchlight.Core.Settings;

namespace Porchlight.Core.Alerts;

/// <summary><see cref="IAlertStateStore"/> backed by <see cref="NotificationSettings"/>.</summary>
public sealed class SettingsAlertStateStore(ISettingsStore settingsStore) : IAlertStateStore
{
    public DateTimeOffset? GetLastFired(string key) =>
        settingsStore.Current.Notifications.LastAlertUtc.TryGetValue(key, out var when) ? when : null;

    public void SetLastFired(string key, DateTimeOffset when) =>
        settingsStore.Update(s => s.Notifications.LastAlertUtc[key] = when);

    public DateTimeOffset? GetRestartPendingSince() => settingsStore.Current.Notifications.RestartPendingSinceUtc;

    public void SetRestartPendingSince(DateTimeOffset? when) =>
        settingsStore.Update(s => s.Notifications.RestartPendingSinceUtc = when);
}
