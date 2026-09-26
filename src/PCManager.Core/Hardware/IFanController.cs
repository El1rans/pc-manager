namespace PCManager.Core.Hardware;

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

    /// <summary>Whether this fan actually accepts software control (some report RPM only).</summary>
    bool CanControl { get; }

    /// <summary>Sets software control to the given duty cycle (0-100). Throws if the underlying
    /// write fails; callers must treat that as fan-control rule 4 (restore all, disable, alert).</summary>
    void SetPercent(double percent);

    /// <summary>Hands control back to the BIOS/EC (LHM's <c>SetDefault</c>). Always safe to call,
    /// including when this fan was never under software control.</summary>
    void RestoreDefault();
}
