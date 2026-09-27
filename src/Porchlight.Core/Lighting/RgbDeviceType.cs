namespace Porchlight.Core.Lighting;

/// <summary>The kind of RGB device, mirrored from OpenRGB's own device type so the UI can choose an
/// icon without depending on OpenRGB.NET's types directly.</summary>
public enum RgbDeviceType
{
    Motherboard,
    Dram,
    Gpu,
    Cooler,
    LedStrip,
    Keyboard,
    Mouse,
    MouseMat,
    Headset,
    HeadsetStand,
    Gamepad,
    Light,
    Speaker,
    Virtual,
    Unknown,
}
