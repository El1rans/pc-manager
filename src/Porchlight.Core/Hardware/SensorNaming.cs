namespace Porchlight.Core.Hardware;

/// <summary>
/// Name-based sensor exclusions that cannot be expressed through <see cref="SensorType"/> alone.
/// </summary>
public static class SensorNaming
{
    /// <summary>
    /// True for a temperature sensor whose value is inverted (lower is hotter), such as Intel's
    /// per-core "Distance to TjMax" (see LHM's <c>IntelCpu.cs</c>) - a value of 5 there means 5 C
    /// <em>below</em> the thermal limit, the opposite of every other temperature sensor. Such a
    /// sensor must never feed the overheat failsafe (it would read as "cold" while the core is
    /// actually near its limit) or be offered as a fan-curve source.
    /// </summary>
    public static bool IsInvertedTemperature(string sensorName) =>
        sensorName.EndsWith("Distance to TjMax", StringComparison.OrdinalIgnoreCase);
}
