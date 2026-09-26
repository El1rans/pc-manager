using LibreHardwareMonitor.Hardware;

namespace PCManager.Core.Hardware;

/// <summary>
/// Thin <see cref="IFanController"/> wrapper around one LHM fan sensor's <see cref="IControl"/>, as
/// specified (rule: the hardware-facing adapter must be thin - all decision logic lives in
/// <see cref="FanControlEngine"/>). Every member here does the least work possible and defers
/// straight to LHM; nothing is cached across calls except the two LHM objects themselves.
/// </summary>
public sealed class LhmFanController : IFanController
{
    private readonly ISensor _fanSensor;
    private readonly IControl _control;

    /// <param name="fanSensor">A <see cref="LibreHardwareMonitor.Hardware.SensorType.Fan"/> sensor
    /// whose <see cref="ISensor.Control"/> is not null.</param>
    public LhmFanController(ISensor fanSensor)
    {
        ArgumentNullException.ThrowIfNull(fanSensor);
        _fanSensor = fanSensor;
        _control = fanSensor.Control ?? throw new ArgumentException("Fan sensor has no control channel.", nameof(fanSensor));
    }

    public string Id => _fanSensor.Identifier.ToString();

    public string Name => _fanSensor.Name;

    public double? CurrentPercent => _control.SoftwareValue is var sw && _control.ControlMode == ControlMode.Software
        ? sw
        : null;

    public bool CanControl => true;

    public void SetPercent(double percent) => _control.SetSoftware((float)Math.Clamp(percent, 0, 100));

    public void RestoreDefault() => _control.SetDefault();
}
