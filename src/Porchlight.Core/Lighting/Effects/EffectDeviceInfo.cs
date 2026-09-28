namespace Porchlight.Core.Lighting.Effects;

/// <summary>One RGB-controllable device, as reported by OpenRGB, narrowed to what the LED effects
/// engine needs (see docs/specs/11-led-effects.md). Distinct from
/// <c>Porchlight.Core.Lighting.RgbDevice</c> - that DTO drops the per-zone matrix map and per-LED
/// names the effects engine needs to build a <see cref="LedLayout"/>.</summary>
/// <param name="Index">The device's index on the OpenRGB SDK server for the lifetime of the
/// effects engine's own connection - see <see cref="IEffectDeviceClient"/>.</param>
/// <param name="Name">Display name, as reported by OpenRGB. Used as the persisted key in
/// <see cref="EffectAssignment.DeviceKey"/> and passed to <see cref="IDeviceExclusionProvider"/>,
/// since OpenRGB's own device index is only stable for one connection's lifetime.</param>
/// <param name="LedCount">Total LEDs across every zone on the device.</param>
/// <param name="Leds">Every LED on the device, in device-LED-index order.</param>
/// <param name="Zones">The device's lighting zones, in zone-index order.</param>
/// <param name="Modes">Every lighting mode the device supports.</param>
/// <param name="ActiveModeIndex">Index into <see cref="Modes"/> of the mode active when this was
/// read - what <see cref="EffectEngine"/> restores the device to on <c>Stop</c>.</param>
public sealed record EffectDeviceInfo(
    int Index,
    string Name,
    int LedCount,
    IReadOnlyList<EffectLedInfo> Leds,
    IReadOnlyList<EffectZoneInfo> Zones,
    IReadOnlyList<EffectModeInfo> Modes,
    int ActiveModeIndex);
