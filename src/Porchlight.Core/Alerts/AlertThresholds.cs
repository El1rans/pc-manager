namespace Porchlight.Core.Alerts;

/// <summary>Named thresholds and windows for <see cref="AlertEvaluator"/>. Defaults are the
/// shipping values; tests may pass their own.</summary>
public sealed record AlertThresholds
{
    /// <summary>CPU temperature (Celsius) above which the sustain timer runs.</summary>
    public double CpuTemperatureC { get; init; } = 90;

    /// <summary>GPU temperature (Celsius) above which the sustain timer runs.</summary>
    public double GpuTemperatureC { get; init; } = 85;

    /// <summary>How long a temperature must stay above its threshold before alerting.</summary>
    public TimeSpan TemperatureSustain { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>How long a restart must have been pending before alerting.</summary>
    public TimeSpan RestartPendingAfter { get; init; } = TimeSpan.FromDays(3);

    /// <summary>Minimum gap between two alerts with the same kind and subject.</summary>
    public TimeSpan Throttle { get; init; } = TimeSpan.FromHours(24);
}
