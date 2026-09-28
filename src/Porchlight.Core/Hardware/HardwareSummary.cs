namespace Porchlight.Core.Hardware;

/// <summary>One tile's worth of data for the Hardware page's "At a glance" summary strip (spec 10).
/// <see cref="Value"/> is null when nothing suitable was found in the snapshot, in which case the
/// tile is not shown.</summary>
/// <param name="Title">Tile title, e.g. "CPU temperature".</param>
/// <param name="Value">Primary value already formatted with its unit.</param>
/// <param name="Detail">Secondary line, e.g. the hot-spot reading or which sensor was used.</param>
/// <param name="Severity">Drives the tile's icon/color (never color alone, per spec 00).</param>
public sealed record HardwareSummaryTile(string Title, string Value, string? Detail, HardwareSummarySeverity Severity);

public enum HardwareSummarySeverity
{
    Normal,
    Caution,
    Critical,
}

/// <summary>
/// Picks the handful of readings shown in the Hardware page's summary strip out of a full
/// <see cref="HardwareSnapshot"/> (spec 10: "CPU temperature (package / Tctl-Tdie), GPU temperature
/// (core; hot spot as detail), CPU package power, and the hottest fan RPM"). Pure and unit tested
/// directly; the failsafe thresholds it uses for severity are the same named constants
/// <see cref="FanControlEngine"/> uses, so the summary strip and fan-control safety banners never
/// disagree about what counts as "hot".
/// </summary>
public static class HardwareSummarySelector
{
    /// <summary>Below the failsafe temperature by at least this many degrees, a temperature tile is
    /// "Caution" rather than "Normal" - an early warning before the failsafe itself would trip.</summary>
    private const double CautionMarginBelowFailsafeC = 15;

    public static IReadOnlyList<HardwareSummaryTile> Build(HardwareSnapshot snapshot, double failsafeTemperatureC)
    {
        var tiles = new List<HardwareSummaryTile>();

        var cpu = FindNode(snapshot.Nodes, HardwareNodeType.Cpu);
        if (cpu is not null && SelectCpuTemperature(cpu) is { } cpuTemp && cpuTemp.Value is not null)
        {
            tiles.Add(new HardwareSummaryTile(
                "CPU temperature",
                SensorFormatter.Format(cpuTemp.Value, SensorType.Temperature),
                cpuTemp.Name,
                Severity(cpuTemp.Value.Value, failsafeTemperatureC)));
        }

        var gpu = FindNode(snapshot.Nodes, HardwareNodeType.Gpu);
        if (gpu is not null && SelectGpuTemperature(gpu) is { } gpuTemp && gpuTemp.Value is not null)
        {
            var hotSpot = SelectGpuHotSpot(gpu);
            var detail = hotSpot is { Value: not null }
                ? $"Hot spot {SensorFormatter.Format(hotSpot.Value, SensorType.Temperature)}"
                : gpuTemp.Name;
            var severity = hotSpot is { Value: not null }
                ? Max(Severity(gpuTemp.Value.Value, failsafeTemperatureC), Severity(hotSpot.Value.Value, failsafeTemperatureC))
                : Severity(gpuTemp.Value.Value, failsafeTemperatureC);
            tiles.Add(new HardwareSummaryTile(
                "GPU temperature",
                SensorFormatter.Format(gpuTemp.Value, SensorType.Temperature),
                detail,
                severity));
        }

        if (cpu is not null && SelectCpuPackagePower(cpu) is { } power && power.Value is not null)
        {
            tiles.Add(new HardwareSummaryTile(
                "CPU package power",
                SensorFormatter.Format(power.Value, SensorType.Power),
                power.Name,
                HardwareSummarySeverity.Normal));
        }

        var hottestFan = SelectHottestFan(snapshot.Nodes);
        if (hottestFan is not null)
        {
            tiles.Add(new HardwareSummaryTile(
                "Hottest fan",
                SensorFormatter.Format(hottestFan.Value, SensorType.Fan),
                hottestFan.Name,
                HardwareSummarySeverity.Normal));
        }

        return tiles;
    }

    /// <summary>Prefers AMD's Tctl/Tdie (the sensor AMD's own tools and the failsafe should agree on)
    /// over Intel's "Package", falling back to the first valid temperature on the node.</summary>
    public static SensorReading? SelectCpuTemperature(HardwareNode cpu)
    {
        var candidates = TemperatureSensors(cpu).ToList();
        return candidates.FirstOrDefault(s => s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                                               || s.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();
    }

    public static SensorReading? SelectGpuTemperature(HardwareNode gpu)
    {
        var candidates = TemperatureSensors(gpu).ToList();
        return candidates.FirstOrDefault(s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();
    }

    public static SensorReading? SelectGpuHotSpot(HardwareNode gpu) =>
        TemperatureSensors(gpu).FirstOrDefault(s => s.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase));

    public static SensorReading? SelectCpuPackagePower(HardwareNode cpu)
    {
        var candidates = AllSensors(cpu).Where(s => s.Type == SensorType.Power).ToList();
        return candidates.FirstOrDefault(s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();
    }

    public static SensorReading? SelectHottestFan(IReadOnlyList<HardwareNode> nodes)
    {
        SensorReading? hottest = null;
        foreach (var node in FlattenNodes(nodes))
        {
            foreach (var sensor in node.Sensors)
            {
                if (sensor.Type != SensorType.Fan || sensor.Value is null || sensor.Value <= 0)
                {
                    continue;
                }

                if (hottest is null || sensor.Value > hottest.Value)
                {
                    hottest = sensor;
                }
            }
        }

        return hottest;
    }

    private static HardwareSummarySeverity Severity(double temperatureC, double failsafeTemperatureC)
    {
        if (temperatureC >= failsafeTemperatureC)
        {
            return HardwareSummarySeverity.Critical;
        }

        return temperatureC >= failsafeTemperatureC - CautionMarginBelowFailsafeC
            ? HardwareSummarySeverity.Caution
            : HardwareSummarySeverity.Normal;
    }

    private static HardwareSummarySeverity Max(HardwareSummarySeverity a, HardwareSummarySeverity b) => a > b ? a : b;

    private static IEnumerable<SensorReading> TemperatureSensors(HardwareNode node) =>
        AllSensors(node).Where(s => s.Type == SensorType.Temperature && !SensorNaming.IsInvertedTemperature(s.Name));

    private static IEnumerable<SensorReading> AllSensors(HardwareNode node)
    {
        foreach (var sensor in node.Sensors)
        {
            yield return sensor;
        }

        foreach (var child in node.Children)
        {
            foreach (var sensor in AllSensors(child))
            {
                yield return sensor;
            }
        }
    }

    private static HardwareNode? FindNode(IReadOnlyList<HardwareNode> nodes, HardwareNodeType type) =>
        FlattenNodes(nodes).FirstOrDefault(n => n.Type == type);

    private static IEnumerable<HardwareNode> FlattenNodes(IReadOnlyList<HardwareNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenNodes(node.Children))
            {
                yield return child;
            }
        }
    }
}
