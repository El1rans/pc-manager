namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// Whether a device should be left alone by the LED effects engine - the same per-device "Don't
/// control this device" exclusion the Lighting page's own "Apply to all"/"Turn off all" honour
/// (see docs/specs/05-lighting.md addendum "Per-device exclusion",
/// docs/specs/11-led-effects.md). <see cref="SettingsDeviceExclusionProvider"/> is the real,
/// settings-backed implementation registered via DI; <see cref="EffectEngine"/> depends only on
/// this interface.
/// </summary>
public interface IDeviceExclusionProvider
{
    /// <summary>Whether the device named <paramref name="deviceName"/> (matching
    /// <c>Porchlight.Core.Lighting.RgbDevice.Name</c> / <see cref="EffectDeviceInfo.Name"/>) is
    /// excluded from engine-driven control.</summary>
    bool IsExcluded(string deviceName);
}
