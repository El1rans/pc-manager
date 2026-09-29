using Porchlight.Core.Hardware;
using Porchlight.Core.Settings;

namespace Porchlight.Core.Checkup.Sections;

/// <summary>CPU and graphics-card temperatures from the latest hardware snapshot. Left out when the
/// hardware service is not ready or has no temperature sensors.</summary>
public sealed class TemperatureCheckupSection : ICheckupSection
{
    /// <summary>A reading this close to the failsafe temperature counts as "needs attention".</summary>
    private const double CautionMarginBelowFailsafeC = 15;

    private readonly IHardwareService _hardware;
    private readonly ISettingsStore _settings;

    public TemperatureCheckupSection(IHardwareService hardware, ISettingsStore settings)
    {
        _hardware = hardware;
        _settings = settings;
    }

    public string Title => "Temperatures";

    public int Order => 500;

    public Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken)
    {
        var snapshot = _hardware.Latest;
        if (snapshot.Status != HardwareStatus.Ready)
        {
            return Task.FromResult<CheckupSectionResult?>(null);
        }

        var failsafe = _settings.Current.Hardware.FailsafeTemperatureC;
        var severity = CheckupSeverity.Ok;
        List<string> lines = [];

        foreach (var (label, type, select) in new (string, HardwareNodeType, Func<HardwareNode, SensorReading?>)[]
        {
            ("Processor", HardwareNodeType.Cpu, HardwareSummarySelector.SelectCpuTemperature),
            ("Graphics card", HardwareNodeType.Gpu, HardwareSummarySelector.SelectGpuTemperature),
        })
        {
            var node = Flatten(snapshot.Nodes).FirstOrDefault(n => n.Type == type);
            if (node is null || select(node) is not { Value: { } value })
            {
                continue;
            }

            lines.Add($"{label}: {SensorFormatter.Format(value, SensorType.Temperature)}");
            if (value >= failsafe)
            {
                severity = CheckupSeverity.Problem;
            }
            else if (value >= failsafe - CautionMarginBelowFailsafeC && severity < CheckupSeverity.NeedsAttention)
            {
                severity = CheckupSeverity.NeedsAttention;
            }
        }

        return Task.FromResult<CheckupSectionResult?>(
            lines.Count == 0 ? null : new CheckupSectionResult(Title, severity, lines));
    }

    private static IEnumerable<HardwareNode> Flatten(IReadOnlyList<HardwareNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
