namespace PCManager.Core.Monitoring;

/// <summary>
/// Aggregates raw <c>GPU Engine / Utilization Percentage</c> counter deltas (one per process/engine
/// instance) into a single GPU utilization percentage, the same way Task Manager does: sum the
/// per-instance values within each engine type (<c>engtype_3D</c>, <c>engtype_VideoDecode</c>, ...),
/// then report the busiest engine type. A pure function over instance name/value pairs so it is
/// unit-testable without touching real performance counters.
/// </summary>
public static class GpuEngineAggregator
{
    private const string EngineTypeMarker = "engtype_";

    public static double Aggregate(IEnumerable<(string InstanceName, double Value)> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var totalsByEngineType = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (instanceName, value) in samples)
        {
            var engineType = ExtractEngineType(instanceName);
            if (engineType is null)
            {
                continue;
            }

            totalsByEngineType.TryGetValue(engineType, out var runningTotal);
            totalsByEngineType[engineType] = runningTotal + value;
        }

        return totalsByEngineType.Count == 0 ? 0 : Math.Clamp(totalsByEngineType.Values.Max(), 0, 100);
    }

    /// <summary>Extracts the engine type suffix (e.g. <c>3D</c>) from a GPU Engine instance name
    /// such as <c>pid_1234_luid_0x...._phys_0_eng_0_engtype_3D</c>. Null if the instance name has no
    /// recognizable engine type.</summary>
    public static string? ExtractEngineType(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName);

        var index = instanceName.IndexOf(EngineTypeMarker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var engineType = instanceName[(index + EngineTypeMarker.Length)..];
        return engineType.Length == 0 ? null : engineType;
    }
}
