using LibreHardwareMonitor.Hardware;

namespace PCManager.Core.Hardware;

/// <summary>
/// Thin <see cref="IFanController"/> wrapper around one LHM fan control channel, as specified (rule:
/// the hardware-facing adapter must be thin - all decision logic lives in
/// <see cref="FanControlEngine"/>). Every member here does the least work possible and defers
/// straight to LHM; nothing is cached across calls except the LHM objects themselves.
/// </summary>
/// <remarks>
/// In LHM 0.9.6, a fan's duty-cycle control lives on a <em>separate</em> sensor
/// (<see cref="LibreHardwareMonitor.Hardware.SensorType.Control"/>) from its RPM reading
/// (<see cref="LibreHardwareMonitor.Hardware.SensorType.Fan"/>) - see <c>SuperIOHardware.CreateControlSensors</c>,
/// <c>NvidiaGpu</c>'s <c>_controls</c>, and <c>AmdGpu</c>'s <c>_controlSensor</c>. Only the control
/// sensor's <see cref="ISensor.Control"/> is ever non-null; the RPM sensor's is always null. This
/// controller therefore wraps the <em>control</em> sensor, and optionally pairs it with the RPM
/// sensor that shares its <see cref="ISensor.Index"/> on the same hardware, purely for display.
/// </remarks>
public sealed class LhmFanController : IFanController
{
    private readonly ISensor _controlSensor;
    private readonly IControl _control;
    private readonly ISensor? _rpmSensor;

    /// <param name="controlSensor">A <see cref="LibreHardwareMonitor.Hardware.SensorType.Control"/>
    /// sensor whose <see cref="ISensor.Control"/> is not null.</param>
    /// <param name="rpmSensor">The RPM sensor sharing this control's index on the same hardware, if
    /// one exists (optional - some controllable fans report no tachometer).</param>
    public LhmFanController(ISensor controlSensor, ISensor? rpmSensor)
    {
        ArgumentNullException.ThrowIfNull(controlSensor);
        _controlSensor = controlSensor;
        _control = controlSensor.Control
            ?? throw new ArgumentException("Control sensor has no control channel.", nameof(controlSensor));
        _rpmSensor = rpmSensor;
    }

    public string Id => _controlSensor.Identifier.ToString();

    public string Name => _rpmSensor?.Name ?? _controlSensor.Name;

    public double? CurrentPercent => _controlSensor.Value;

    public bool IsUnderSoftwareControl => _control.ControlMode == ControlMode.Software;

    public bool CanControl => true;

    public string? RpmSensorId => _rpmSensor?.Identifier.ToString();

    public double MinSoftwarePercent => _control.MinSoftwareValue;

    public double MaxSoftwarePercent => _control.MaxSoftwareValue;

    public void SetPercent(double percent)
    {
        // NaN would otherwise pass straight through Math.Clamp unmodified (IEEE 754 comparisons
        // against NaN are always false) and reach the driver as a garbage value - treat it as the
        // safest thing we can do instead: full speed.
        var safePercent = double.IsNaN(percent) ? 100 : percent;
        var lowerBound = Math.Max(FanControlOptions.LowestAllowedMinPercent, _control.MinSoftwareValue);
        var upperBound = Math.Max(lowerBound, _control.MaxSoftwareValue);
        var clamped = Math.Clamp(safePercent, lowerBound, upperBound);

        // Known LHM quirk: SetSoftware() switches ControlMode to Software *and* writes
        // SoftwareValue in the same call, but several SuperIO backends (Nct677X, IT87XX) only
        // apply the new value on the register write that follows a mode change, meaning the very
        // first SetSoftware() after SetDefault()/construction can silently keep the previous
        // (often 0) duty cycle for one tick. FanControlManager's read-back verification (rule 3/S3)
        // catches a channel that never converges; there is nothing more this thin adapter should
        // do about it without becoming a decision-maker itself.
        _control.SetSoftware((float)clamped);
    }

    public void RestoreDefault() => _control.SetDefault();
}
