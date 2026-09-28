namespace Porchlight.Core.Hardware;

/// <summary>One controllable fan. The LHM-backed implementation maps <see cref="SetPercent"/> and
/// <see cref="RestoreDefault"/> onto <c>ISensor.Control.SetSoftware</c>/<c>SetDefault</c>; this
/// interface keeps every other piece of fan-control logic (<see cref="FanControlEngine"/>,
/// <c>FanControlManager</c>) free of any LHM type, so they can be unit tested with a fake.</summary>
public interface IFanController
{
    /// <summary>Stable identifier (LHM control sensor identifier string in the real adapter).</summary>
    string Id { get; }

    /// <summary>Display name, e.g. "CPU fan".</summary>
    string Name { get; }

    /// <summary>Current duty cycle, 0-100, or null if unknown.</summary>
    double? CurrentPercent { get; }

    /// <summary>Whether this control channel currently reports itself as under software control
    /// (LHM's <c>IControl.ControlMode == ControlMode.Software</c>). Used only to verify that
    /// <see cref="RestoreDefault"/> actually took effect - some SuperIO backends can accept the call
    /// without error yet leave the channel in software mode (the same class of quirk
    /// <see cref="SetPercent"/>'s remarks describe for the opposite direction).</summary>
    bool IsUnderSoftwareControl { get; }

    /// <summary>Whether this fan actually accepts software control (some report RPM only).</summary>
    bool CanControl { get; }

    /// <summary>
    /// Id of the paired RPM/tachometer sensor shown alongside this control in the sensors tree and
    /// fan card, or null if this control channel has no matching tachometer (rare, but the LHM
    /// control and fan/RPM sensors are reported separately and are only "paired" by convention -
    /// same hardware, same sensor index).
    /// </summary>
    string? RpmSensorId { get; }

    /// <summary>Lowest duty cycle this control channel accepts, per the hardware/driver (LHM's
    /// <c>IControl.MinSoftwareValue</c>). <see cref="SetPercent"/> callers must not assume 0 is
    /// always valid.</summary>
    double MinSoftwarePercent { get; }

    /// <summary>Highest duty cycle this control channel accepts (LHM's
    /// <c>IControl.MaxSoftwareValue</c>) - usually 100, but not guaranteed.</summary>
    double MaxSoftwarePercent { get; }

    /// <summary>Sets software control to the given duty cycle (0-100). Throws if the underlying
    /// write fails; callers must treat that as fan-control rule 4 (restore all, disable, alert).</summary>
    void SetPercent(double percent);

    /// <summary>Hands control back to the BIOS/EC (LHM's <c>SetDefault</c>). Always safe to call,
    /// including when this fan was never under software control.</summary>
    void RestoreDefault();
}
