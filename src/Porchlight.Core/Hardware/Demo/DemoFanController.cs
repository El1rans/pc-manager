#if DEBUG
namespace Porchlight.Core.Hardware.Demo;

/// <summary>DEBUG-only fake <see cref="IFanController"/> for the "demo data" mode (see
/// <c>Monitoring.Demo.DemoDataMode</c>) - reports a fixed duty cycle and never actually controls
/// anything: <see cref="SetPercent"/> and <see cref="RestoreDefault"/> are no-ops. Never touches
/// real hardware, so it is safe to exercise the Hardware page's fan controls while capturing
/// documentation screenshots.</summary>
internal sealed class DemoFanController(string id, string name, double currentPercent, string? rpmSensorId, HardwareNodeType nodeType = HardwareNodeType.Motherboard) : IFanController
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public double? CurrentPercent { get; private set; } = currentPercent;
    public bool IsUnderSoftwareControl { get; private set; }
    public bool CanControl => true;
    public string? RpmSensorId { get; } = rpmSensorId;
    public HardwareNodeType NodeType { get; } = nodeType;
    public double MinSoftwarePercent => 20;
    public double MaxSoftwarePercent => 100;

    public void SetPercent(double percent)
    {
        CurrentPercent = percent;
        IsUnderSoftwareControl = true;
    }

    public void RestoreDefault()
    {
        IsUnderSoftwareControl = false;
    }
}
#endif
