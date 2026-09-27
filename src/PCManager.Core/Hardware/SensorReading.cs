namespace PCManager.Core.Hardware;

/// <summary>One sensor's current value plus its observed min/max, as shown on the sensors tab and
/// consumed by <see cref="FanControlEngine"/> for fan-curve sources and failsafe checks.</summary>
/// <param name="Id">Stable identifier (LHM sensor identifier string in the real adapter).</param>
/// <param name="Name">Display name, e.g. "CPU Package".</param>
/// <param name="Type">Determines the unit shown (C, RPM, %, MHz, V, W).</param>
/// <param name="Value">Current value, or null if the sensor has no reading right now.</param>
/// <param name="Min">Smallest value observed since the last reset.</param>
/// <param name="Max">Largest value observed since the last reset.</param>
/// <param name="TimestampUtc">When this reading was taken; used to detect a stale sensor
/// (<see cref="FanControlOptions.StaleSensorThreshold"/>).</param>
public sealed record SensorReading(
    string Id,
    string Name,
    SensorType Type,
    double? Value,
    double? Min,
    double? Max,
    DateTimeOffset TimestampUtc);
