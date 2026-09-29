namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the tray icon, background alerts and scheduled update checks. See
/// <c>docs/specs/15-tray-and-alerts.md</c>.</summary>
public sealed class NotificationSettings
{
    /// <summary>"Keep Porchlight running in the tray when I close the window".</summary>
    public bool KeepRunningInTray { get; set; } = true;

    /// <summary>True once the one-time "Porchlight is still running here" balloon was shown.</summary>
    public bool TrayHintShown { get; set; }

    /// <summary>"Tell me when a drive is almost full".</summary>
    public bool AlertLowDisk { get; set; } = true;

    /// <summary>"Tell me when my PC is running too hot".</summary>
    public bool AlertTemperature { get; set; } = true;

    /// <summary>"Tell me when app updates are ready".</summary>
    public bool AlertUpdates { get; set; } = true;

    /// <summary>"Remind me when my PC has needed a restart for a few days".</summary>
    public bool AlertRestartPending { get; set; } = true;

    /// <summary>"Check for app updates every day / week / never".</summary>
    public UpdateCheckSchedule UpdateCheckSchedule { get; set; } = UpdateCheckSchedule.Daily;

    /// <summary>When the last scheduled update check finished.</summary>
    public DateTimeOffset? LastScheduledUpdateCheckUtc { get; set; }

    /// <summary>When each alert last fired, keyed <c>"&lt;kind&gt;:&lt;subject&gt;"</c>; drives the
    /// once-per-24-hours throttle across app restarts.</summary>
    public Dictionary<string, DateTimeOffset> LastAlertUtc { get; set; } = [];

    /// <summary>When Porchlight first noticed a pending restart, or null when none is pending.</summary>
    public DateTimeOffset? RestartPendingSinceUtc { get; set; }
}
