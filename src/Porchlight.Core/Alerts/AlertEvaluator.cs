using System.Globalization;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Alerts;

/// <summary>
/// Pure alert decision logic: thresholds, sustain windows and once-per-24-hours throttling, with
/// "now" from an injected <see cref="TimeProvider"/> and persistent memory behind
/// <see cref="IAlertStateStore"/>. Does no I/O of its own. See
/// <c>docs/specs/15-tray-and-alerts.md</c>. Thread-safe (one lock).
/// </summary>
public sealed class AlertEvaluator
{
    private const string CpuSubject = "cpu";
    private const string GpuSubject = "gpu";
    private const string UpdatesSubject = "updates";
    private const string RestartSubject = "restart";

    private readonly TimeProvider _timeProvider;
    private readonly IAlertStateStore _state;
    private readonly AlertThresholds _thresholds;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _hotSince = [];

    public AlertEvaluator(TimeProvider timeProvider, IAlertStateStore state, AlertThresholds? thresholds = null)
    {
        _timeProvider = timeProvider;
        _state = state;
        _thresholds = thresholds ?? new AlertThresholds();
    }

    public static string BuildKey(AlertKind kind, string subject) =>
        string.Create(CultureInfo.InvariantCulture, $"{kind}:{subject}");

    /// <summary>Alerts to show right now for low disk, temperature and a pending restart. Each one
    /// returned is already recorded as fired (so it will not be returned again for 24 hours).</summary>
    public IReadOnlyList<Alert> Evaluate(AlertInputs inputs, AlertPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(preferences);

        var now = _timeProvider.GetUtcNow();
        var alerts = new List<Alert>();

        lock (_gate)
        {
            if (preferences.LowDisk)
            {
                foreach (var drive in inputs.Drives)
                {
                    if (DriveThresholds.IsLow(drive.FreeBytes, drive.TotalBytes))
                    {
                        AddIfNotThrottled(alerts, now, LowDiskAlert(drive));
                    }
                }
            }

            EvaluateTemperature(alerts, now, CpuSubject, "processor", inputs.CpuTemperatureC, _thresholds.CpuTemperatureC, preferences.Temperature);
            EvaluateTemperature(alerts, now, GpuSubject, "graphics card", inputs.GpuTemperatureC, _thresholds.GpuTemperatureC, preferences.Temperature);
            EvaluateRestart(alerts, now, inputs.RestartPending, preferences.RestartPending);
        }

        return alerts;
    }

    /// <summary>The "updates are ready" alert, or null. Call only after a scheduled check finished.</summary>
    public Alert? EvaluateUpdates(int pendingUpdateCount, AlertPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (!preferences.Updates || pendingUpdateCount <= 0)
        {
            return null;
        }

        var title = pendingUpdateCount == 1
            ? "1 app update is ready"
            : string.Create(CultureInfo.InvariantCulture, $"{pendingUpdateCount} app updates are ready");
        var alert = new Alert(
            AlertKind.UpdatesAvailable,
            UpdatesSubject,
            title,
            "Open Porchlight to install them when you like.",
            AlertTarget.Updates);

        var now = _timeProvider.GetUtcNow();
        var alerts = new List<Alert>();
        lock (_gate)
        {
            AddIfNotThrottled(alerts, now, alert);
        }

        return alerts.Count > 0 ? alerts[0] : null;
    }

    private void EvaluateTemperature(
        List<Alert> alerts, DateTimeOffset now, string subject, string partName,
        double? reading, double thresholdC, bool enabled)
    {
        // No reading (sensors need admin + the driver), a cool reading, or the alert being off all
        // reset the sustain timer: only an unbroken run above the threshold counts.
        if (!enabled || reading is null || reading.Value <= thresholdC)
        {
            _hotSince.Remove(subject);
            return;
        }

        if (!_hotSince.TryGetValue(subject, out var since))
        {
            _hotSince[subject] = now;
            return;
        }

        if (now - since < _thresholds.TemperatureSustain)
        {
            return;
        }

        AddIfNotThrottled(alerts, now, new Alert(
            AlertKind.HighTemperature,
            subject,
            "Your PC is running hot",
            $"The {partName} has been very hot for a few minutes. Make sure the vents are not " +
            "blocked. If this keeps happening, tell your helper.",
            AlertTarget.Hardware));
    }

    private void EvaluateRestart(List<Alert> alerts, DateTimeOffset now, bool restartPending, bool enabled)
    {
        // Tracked even while the alert is off, so turning it on later knows how long it has waited.
        var since = _state.GetRestartPendingSince();
        if (!restartPending)
        {
            if (since is not null)
            {
                _state.SetRestartPendingSince(null);
            }

            return;
        }

        if (since is null)
        {
            _state.SetRestartPendingSince(now);
            return;
        }

        var waited = now - since.Value;
        if (!enabled || waited <= _thresholds.RestartPendingAfter)
        {
            return;
        }

        var days = (int)waited.TotalDays;
        AddIfNotThrottled(alerts, now, new Alert(
            AlertKind.RestartPending,
            RestartSubject,
            "Your PC needs a restart",
            string.Create(
                CultureInfo.InvariantCulture,
                $"It has been waiting for {days} days. Restart when you have a moment so updates can finish."),
            AlertTarget.Dashboard));
    }

    private static Alert LowDiskAlert(DriveSnapshot drive)
    {
        var name = drive.Name.TrimEnd('\\', '/');
        return new Alert(
            AlertKind.LowDisk,
            drive.Name,
            $"Drive {name} is almost full",
            $"Only {ByteFormatter.FormatBytes(drive.FreeBytes)} is free. Open Porchlight to see your drives.",
            AlertTarget.Dashboard);
    }

    private void AddIfNotThrottled(List<Alert> alerts, DateTimeOffset now, Alert alert)
    {
        var key = alert.Key;
        var last = _state.GetLastFired(key);

        // last > now: the clock went backwards; treat as expired rather than muting the alert.
        if (last is not null && last <= now && now - last.Value < _thresholds.Throttle)
        {
            return;
        }

        _state.SetLastFired(key, now);
        alerts.Add(alert);
    }
}
