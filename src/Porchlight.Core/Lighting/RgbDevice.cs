namespace Porchlight.Core.Lighting;

/// <summary>One RGB-controllable device as reported by OpenRGB (a motherboard, a RAM stick, a
/// keyboard, ...).</summary>
/// <param name="Index">The device's index on the OpenRGB SDK server; stable for the lifetime of
/// its connection, passed back to every per-device <see cref="ILightingService"/> call.</param>
/// <param name="Name">Display name, as reported by OpenRGB.</param>
/// <param name="Type">The kind of device, used to choose an icon.</param>
/// <param name="Vendor">Vendor name, when OpenRGB reports one.</param>
/// <param name="Modes">Every lighting mode the device supports.</param>
/// <param name="ActiveMode">Name of the mode currently active on the device.</param>
/// <param name="LedCount">Total number of LEDs across all the device's zones.</param>
/// <param name="Zones">The device's lighting zones.</param>
public sealed record RgbDevice(
    int Index,
    string Name,
    RgbDeviceType Type,
    string? Vendor,
    IReadOnlyList<RgbMode> Modes,
    string ActiveMode,
    int LedCount,
    IReadOnlyList<RgbZone> Zones);
