namespace PCManager.Core.Monitoring;

/// <summary>
/// Aggregates raw <c>GPU Engine / Utilization Percentage</c> counter deltas (one instance per
/// process using a given physical engine) into a single GPU utilization percentage, the same way
/// Task Manager does: sum the per-process values that share the same physical engine (so multiple
/// processes driving the same engine add up correctly), then report the busiest physical engine
/// across every adapter. A pure function over instance name/value pairs so it is unit-testable
/// without touching real performance counters.
/// </summary>
public static class GpuEngineAggregator
{
    private const string PidPrefix = "pid_";

    public static double Aggregate(IEnumerable<(string InstanceName, double Value)> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var totalsByPhysicalEngine = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (instanceName, value) in samples)
        {
            var engineKey = ExtractPhysicalEngineKey(instanceName);
            if (engineKey is null)
            {
                continue;
            }

            totalsByPhysicalEngine.TryGetValue(engineKey, out var runningTotal);
            totalsByPhysicalEngine[engineKey] = runningTotal + value;
        }

        return totalsByPhysicalEngine.Count == 0 ? 0 : Math.Clamp(totalsByPhysicalEngine.Values.Max(), 0, 100);
    }

    /// <summary>
    /// Strips the <c>pid_&lt;n&gt;_</c> prefix from a GPU Engine instance name, leaving the physical
    /// engine identity shared by every process using that same engine - e.g.
    /// <c>pid_1234_luid_0x00000000_0x0000C2A3_phys_0_eng_0_engtype_3D</c> becomes
    /// <c>luid_0x00000000_0x0000C2A3_phys_0_eng_0_engtype_3D</c>. Two processes on the same
    /// physical engine (same LUID/phys/eng/engtype) collapse to the same key so their utilization
    /// sums; a second adapter's engines (a different LUID) get their own distinct keys, so a
    /// multi-GPU machine still reports the single busiest physical engine rather than conflating
    /// unrelated adapters. Instance names without a recognizable <c>pid_</c> prefix (e.g. the
    /// category's own "_Total" instance) return null and are excluded.
    /// </summary>
    public static string? ExtractPhysicalEngineKey(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName);

        if (!instanceName.StartsWith(PidPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var afterPrefix = instanceName[PidPrefix.Length..];
        var underscoreAfterPid = afterPrefix.IndexOf('_');
        if (underscoreAfterPid < 0)
        {
            return null;
        }

        var physicalEngineKey = afterPrefix[(underscoreAfterPid + 1)..];
        return physicalEngineKey.Length == 0 ? null : physicalEngineKey;
    }
}
