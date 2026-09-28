using Porchlight.Core.Hardware;

namespace Porchlight.Core.Lighting.Effects;

/// <summary>Reads the current CPU temperature out of <see cref="IHardwareService.Latest"/> for
/// <see cref="EffectEngine"/>'s per-tick <see cref="IEffectContext"/>, reusing the same "which
/// sensor counts as THE CPU temperature" rule the Hardware page's summary strip uses
/// (<see cref="HardwareSummarySelector.SelectCpuTemperature"/>) so they never disagree. Pure and
/// unit tested directly.</summary>
public static class CpuTemperatureReader
{
    public static double? Read(HardwareSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var cpu = FindCpuNode(snapshot.Nodes);
        if (cpu is null)
        {
            return null;
        }

        return HardwareSummarySelector.SelectCpuTemperature(cpu)?.Value;
    }

    private static HardwareNode? FindCpuNode(IReadOnlyList<HardwareNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Type == HardwareNodeType.Cpu)
            {
                return node;
            }

            var found = FindCpuNode(node.Children);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
