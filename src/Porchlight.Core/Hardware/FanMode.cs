namespace Porchlight.Core.Hardware;

/// <summary>How a single fan is driven.</summary>
public enum FanMode
{
    /// <summary>BIOS/EC control; Porchlight never calls <see cref="IFanController.SetPercent"/>.</summary>
    Default,

    /// <summary>Held at a fixed duty cycle (still subject to the safety floor and failsafes).</summary>
    Fixed,

    /// <summary>Driven by a <see cref="FanCurve"/> reading a chosen temperature sensor.</summary>
    Curve,
}
