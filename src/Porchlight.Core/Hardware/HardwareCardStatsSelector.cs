namespace Porchlight.Core.Hardware;

/// <summary>
/// Picks the 2 most useful live stats shown next to a hardware card's name when it is collapsed on
/// the Hardware page (spec 10 addendum: collapsed-card stats) - e.g.
/// "AMD Ryzen 7 5800X3D      69.8 °C · 22 %". Each hardware type prefers a different pair of
/// sensors; reuses <see cref="HardwareSummarySelector"/>'s sensor-picking helpers for CPU/GPU
/// temperature so the summary strip and a collapsed CPU/GPU card never disagree about which sensor
/// is "the" temperature. Pure and unit tested directly; formatting is delegated to
/// <see cref="SensorFormatter"/> like every other sensor value on this page.
/// </summary>
public static class HardwareCardStatsSelector
{
    /// <summary>Formatted stat strings for <paramref name="node"/>'s collapsed card header, most
    /// useful first. Never more than 2 entries; fewer (or none) when the node does not have that
    /// many usable readings (spec: "if fewer than 2 available show what exists; if none, show
    /// nothing").</summary>
    public static IReadOnlyList<string> Build(HardwareNode node)
    {
        IEnumerable<SensorReading?> candidates = node.Type switch
        {
            HardwareNodeType.Cpu => [HardwareSummarySelector.SelectCpuTemperature(node), SelectByName(node, SensorType.Load, "CPU Total")],
            HardwareNodeType.Gpu => [HardwareSummarySelector.SelectGpuTemperature(node), SelectByName(node, SensorType.Load, "Core")],
            HardwareNodeType.Memory => [SelectFirst(node, SensorType.Load), SelectByName(node, SensorType.Data, "Memory Used")],
            HardwareNodeType.Storage => [SelectFirst(node, SensorType.Temperature), SelectByName(node, SensorType.Load, "Used Space")],
            HardwareNodeType.Motherboard => [SelectHighest(node, SensorType.Temperature), SelectHighest(node, SensorType.Fan)],
            HardwareNodeType.Network => NetworkStats(node),
            _ => [SelectFirst(node, SensorType.Temperature), SelectFirst(node, SensorType.Load)],
        };

        return candidates
            .Where(r => r?.Value is not null)
            .Take(2)
            .Select(r => SensorFormatter.Format(r!.Value, r.Type))
            .ToList();
    }

    private static IEnumerable<SensorReading?> NetworkStats(HardwareNode node)
    {
        var throughput = AllSensors(node).Where(s => s.Type == SensorType.Throughput).ToList();
        var download = throughput.FirstOrDefault(s => s.Name.Contains("Download", StringComparison.OrdinalIgnoreCase));
        var upload = throughput.FirstOrDefault(s => s.Name.Contains("Upload", StringComparison.OrdinalIgnoreCase));

        download ??= throughput.FirstOrDefault(s => s.Id != upload?.Id);
        upload ??= throughput.FirstOrDefault(s => s.Id != download?.Id);

        return [download, upload];
    }

    private static SensorReading? SelectFirst(HardwareNode node, SensorType type) =>
        AllSensors(node).FirstOrDefault(s => s.Type == type);

    private static SensorReading? SelectByName(HardwareNode node, SensorType type, string nameContains)
    {
        var candidates = AllSensors(node).Where(s => s.Type == type).ToList();
        return candidates.FirstOrDefault(s => s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();
    }

    /// <summary>Highest-value sensor of <paramref name="type"/> under <paramref name="node"/>,
    /// ignoring zero/negative readings (an unconnected fan header or idle rail) and, for
    /// temperatures, any inverted sensor (spec: never a fan-curve/safety source, and not a useful
    /// "hottest" reading either).</summary>
    private static SensorReading? SelectHighest(HardwareNode node, SensorType type)
    {
        SensorReading? best = null;
        foreach (var sensor in AllSensors(node))
        {
            if (sensor.Type != type || sensor.Value is null || sensor.Value <= 0)
            {
                continue;
            }

            if (type == SensorType.Temperature && SensorNaming.IsInvertedTemperature(sensor.Name))
            {
                continue;
            }

            if (best is null || sensor.Value > best.Value)
            {
                best = sensor;
            }
        }

        return best;
    }

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
}
