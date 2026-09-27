using NativeColor = OpenRGB.NET.Color;
using NativeDevice = OpenRGB.NET.Device;
using NativeDeviceType = OpenRGB.NET.DeviceType;

namespace PCManager.Core.Lighting;

/// <inheritdoc cref="IOpenRgbClient"/>
public sealed class OpenRgbClientAdapter : IOpenRgbClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;
    private OpenRGB.NET.OpenRgbClient? _client;

    public OpenRgbClientAdapter(string host, int port, int timeoutMs)
    {
        _host = host;
        _port = port;
        _timeoutMs = timeoutMs;
    }

    public bool Connected => _client?.Connected == true;

    public void Connect()
    {
        // A fresh client each time: OpenRgbClient has no reconnect of its own once its socket has
        // died, so reconnecting means disposing the old one and creating a new one.
        _client?.Dispose();
        _client = null;
        _client = new OpenRGB.NET.OpenRgbClient(
            ip: _host, port: _port, name: "PC Manager", autoConnect: true, timeoutMs: _timeoutMs);
    }

    public IReadOnlyList<RgbDevice> GetAllControllerData() =>
        Array.ConvertAll(RequireClient().GetAllControllerData(), ToRgbDevice);

    public IReadOnlyList<string> GetProfiles() => RequireClient().GetProfiles();

    public void LoadProfile(string name) => RequireClient().LoadProfile(name);

    public void SetMode(int deviceIndex, int modeIndex) => RequireClient().UpdateMode(deviceIndex, modeIndex);

    public void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors)
    {
        var native = new NativeColor[colors.Count];
        for (var i = 0; i < colors.Count; i++)
        {
            native[i] = new NativeColor(colors[i].R, colors[i].G, colors[i].B);
        }

        RequireClient().UpdateLeds(deviceIndex, native);
    }

    public void Dispose() => _client?.Dispose();

    private OpenRGB.NET.OpenRgbClient RequireClient() =>
        _client ?? throw new InvalidOperationException("Not connected to OpenRGB. Call Connect() first.");

    private static RgbDevice ToRgbDevice(NativeDevice device)
    {
        var modes = Array.ConvertAll(device.Modes, m => new RgbMode(m.Index, m.Name));
        var zones = Array.ConvertAll(device.Zones, z => new RgbZone(z.Name, (int)z.LedCount));

        var activeMode = device.ActiveModeIndex >= 0 && device.ActiveModeIndex < device.Modes.Length
            ? device.Modes[device.ActiveModeIndex].Name
            : string.Empty;

        return new RgbDevice(
            Index: device.Index,
            Name: device.Name,
            Type: ToRgbDeviceType(device.Type),
            Vendor: device.Vendor,
            Modes: modes,
            ActiveMode: activeMode,
            LedCount: device.Leds.Length,
            Zones: zones);
    }

    private static RgbDeviceType ToRgbDeviceType(NativeDeviceType type) => type switch
    {
        NativeDeviceType.Motherboard => RgbDeviceType.Motherboard,
        NativeDeviceType.Dram => RgbDeviceType.Dram,
        NativeDeviceType.Gpu => RgbDeviceType.Gpu,
        NativeDeviceType.Cooler => RgbDeviceType.Cooler,
        NativeDeviceType.Ledstrip => RgbDeviceType.LedStrip,
        NativeDeviceType.Keyboard => RgbDeviceType.Keyboard,
        NativeDeviceType.Mouse => RgbDeviceType.Mouse,
        NativeDeviceType.Mousemat => RgbDeviceType.MouseMat,
        NativeDeviceType.Headset => RgbDeviceType.Headset,
        NativeDeviceType.HeadsetStand => RgbDeviceType.HeadsetStand,
        NativeDeviceType.Gamepad => RgbDeviceType.Gamepad,
        NativeDeviceType.Light => RgbDeviceType.Light,
        NativeDeviceType.Speaker => RgbDeviceType.Speaker,
        NativeDeviceType.Virtual => RgbDeviceType.Virtual,
        _ => RgbDeviceType.Unknown,
    };
}
