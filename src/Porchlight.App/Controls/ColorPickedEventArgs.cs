using Porchlight.Core.Lighting;

namespace Porchlight.App.Controls;

/// <summary>Raised by <see cref="ColorWheelPicker.ColorPicked"/> for every interactive change:
/// while the user is dragging the wheel/value slider, and once more, with
/// <see cref="IsFinal"/> true, when the interaction ends. A caller applying this to a device
/// should throttle while <see cref="IsFinal"/> is false (see
/// <see cref="Porchlight.Core.Lighting.ColorApplyRateLimiter"/>) but always apply the final one.</summary>
public sealed class ColorPickedEventArgs(RgbColor color, bool isFinal) : EventArgs
{
    public RgbColor Color { get; } = color;

    public bool IsFinal { get; } = isFinal;
}
