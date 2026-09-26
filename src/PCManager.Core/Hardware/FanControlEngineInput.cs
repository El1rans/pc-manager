namespace PCManager.Core.Hardware;

/// <summary>One evaluation's worth of input for <see cref="FanControlEngine.Evaluate"/>. Contains
/// no LHM types - only plain data - so the engine can be exercised with fake sensors and a fake
/// clock.</summary>
/// <param name="SoftwareControlEnabled">The user's master toggle. When false, every fan is left on
/// (or restored to) default/BIOS control regardless of profiles.</param>
/// <param name="MinPercent">Rule 1 floor; already clamped to
/// <see cref="FanControlOptions.LowestAllowedMinPercent"/> by whoever persisted it.</param>
/// <param name="FailsafeTemperatureC">Rule 2 threshold.</param>
/// <param name="Profiles">One profile per controlled fan (fans left at <see cref="FanMode.Default"/>
/// may be included or omitted; the engine treats a missing fan id the same as Default).</param>
/// <param name="CpuGpuTemperatures">Every current CPU/GPU temperature reading, keyed by sensor id -
/// the set rule 2 watches. A null <see cref="SensorSample.ValueC"/> is treated as "unknown" and
/// never counted towards recovery.</param>
/// <param name="FanSourceTemperatures">Current reading for every sensor id referenced by a
/// <see cref="FanMode.Curve"/> profile's <see cref="FanProfile.SourceSensorId"/>. A fan whose source
/// id is missing from this dictionary is treated as "sensor lost" (rule 3).</param>
public sealed record FanControlEngineInput(
    bool SoftwareControlEnabled,
    double MinPercent,
    double FailsafeTemperatureC,
    IReadOnlyList<FanProfile> Profiles,
    IReadOnlyDictionary<string, SensorSample> CpuGpuTemperatures,
    IReadOnlyDictionary<string, SensorSample> FanSourceTemperatures);

/// <param name="ValueC">Null means "no reading" (sensor present but momentarily unavailable).</param>
/// <param name="TimestampUtc">When this sample was taken; compared against
/// <see cref="FanControlOptions.StaleSensorThreshold"/>.</param>
public sealed record SensorSample(double? ValueC, DateTimeOffset TimestampUtc);
