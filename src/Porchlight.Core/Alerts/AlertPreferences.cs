using Porchlight.Core.Settings;

namespace Porchlight.Core.Alerts;

/// <summary>Which alert types are turned on.</summary>
public sealed record AlertPreferences(bool LowDisk, bool Temperature, bool Updates, bool RestartPending)
{
    public static AlertPreferences FromSettings(NotificationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new AlertPreferences(
            settings.AlertLowDisk, settings.AlertTemperature, settings.AlertUpdates, settings.AlertRestartPending);
    }
}
