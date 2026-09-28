namespace Porchlight.Core.Lighting.Effects;

/// <summary>Picks the mode <see cref="EffectEngine"/> switches a device to before rendering: the
/// per-LED "Direct" mode, mirroring <c>Porchlight.Core.Lighting.LightingModeSelector</c>'s own rule
/// for the Lighting page (kept separate - see <see cref="IEffectDeviceClient"/> - rather than
/// shared, since it operates on <see cref="EffectModeInfo"/> instead of
/// <c>Porchlight.Core.Lighting.RgbMode</c>).</summary>
public static class EffectModeSelector
{
    private const string DirectModeName = "Direct";

    /// <summary>Returns the device's "Direct" mode, if it has one accepting per-LED colors, or
    /// null if the device has none (<see cref="EffectEngine"/> then leaves it on its current mode
    /// and still tries <see cref="IEffectDeviceClient.UpdateLeds"/> - most devices with no explicit
    /// "Direct" mode still accept per-LED colors on whatever mode is active).</summary>
    public static EffectModeInfo? SelectDirectMode(IReadOnlyList<EffectModeInfo> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);

        foreach (var mode in modes)
        {
            if (mode.IsPerLed && string.Equals(mode.Name, DirectModeName, StringComparison.OrdinalIgnoreCase))
            {
                return mode;
            }
        }

        return null;
    }
}
