namespace Porchlight.Core.Alerts;

/// <summary>Persistence for the little state <see cref="AlertEvaluator"/> must remember across app
/// restarts: when each alert last fired and when a restart first became pending.</summary>
public interface IAlertStateStore
{
    DateTimeOffset? GetLastFired(string key);

    void SetLastFired(string key, DateTimeOffset when);

    DateTimeOffset? GetRestartPendingSince();

    void SetRestartPendingSince(DateTimeOffset? when);
}
