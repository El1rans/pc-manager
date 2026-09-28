namespace Porchlight.Core.Hardware;

/// <summary>Named constants for every fan-safety threshold in spec 04, so none of them are magic
/// numbers scattered through <see cref="FanControlEngine"/> or the settings UI.</summary>
public static class FanControlOptions
{
    /// <summary>Default minimum fan percent (rule 1).</summary>
    public const int DefaultMinPercent = 30;

    /// <summary>The user cannot set the minimum fan percent below this (rule 1).</summary>
    public const int LowestAllowedMinPercent = 20;

    /// <summary>Default overheat failsafe temperature, degrees C (rule 2).</summary>
    public const double DefaultFailsafeTemperatureC = 90;

    /// <summary>User-adjustable range for the failsafe temperature (rule 2).</summary>
    public const double LowestAllowedFailsafeTemperatureC = 70;

    public const double HighestAllowedFailsafeTemperatureC = 95;

    /// <summary>Once the overheat failsafe trips, it stays active until every CPU/GPU temperature
    /// is this many degrees below <see cref="FailsafeTemperatureC"/> (rule 2's "recovery band").</summary>
    public const double OverheatRecoveryBandC = 10;

    /// <summary>A fan's source temperature reading older than this counts as stale (rule 3).</summary>
    public static readonly TimeSpan StaleSensorThreshold = TimeSpan.FromSeconds(5);

    /// <summary>A curve only lowers a fan's speed once the temperature has dropped at least this
    /// many degrees below the point where the current speed was set (curve hysteresis).</summary>
    public const double HysteresisBandC = 3;

    /// <summary>Fewest points a valid <see cref="FanCurve"/> may have.</summary>
    public const int MinCurvePoints = 2;

    /// <summary>Most points a valid <see cref="FanCurve"/> may have.</summary>
    public const int MaxCurvePoints = 8;
}
