using NativeColor = OpenRGB.NET.Color;
using NativeColorMode = OpenRGB.NET.ColorMode;
using NativeDevice = OpenRGB.NET.Device;
using NativeZone = OpenRGB.NET.Zone;
using NativeZoneType = OpenRGB.NET.ZoneType;

namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// <see cref="IEffectDeviceClient"/> implemented directly on top of OpenRGB.NET's own socket
/// client, with its own connection to the OpenRGB SDK server - see <see cref="IEffectDeviceClient"/>
/// for why this does not reuse <c>Porchlight.Core.Lighting.OpenRgbClientAdapter</c>.
/// </summary>
public sealed class OpenRgbEffectDeviceClient : IEffectDeviceClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;
    private readonly Dictionary<int, NativeColor[]> _nativeBuffers = [];
    private OpenRGB.NET.OpenRgbClient? _client;

    public OpenRgbEffectDeviceClient(string host, int port, int timeoutMs)
    {
        _host = host;
        _port = port;
        _timeoutMs = timeoutMs;
    }

    public bool Connected => _client?.Connected == true;

    public void Connect()
    {
        if (Connected)
        {
            return;
        }

        _client?.Dispose();
        _client = new OpenRGB.NET.OpenRgbClient(
            ip: _host, port: _port, name: "Porchlight LED effects", autoConnect: false, timeoutMs: _timeoutMs);
        _client.Connect();
    }

    public IReadOnlyList<EffectDeviceInfo> GetAllDevices() =>
        Array.ConvertAll(RequireClient().GetAllControllerData(), ToEffectDevice);

    public void SetMode(int deviceIndex, int modeIndex) => RequireClient().UpdateMode(deviceIndex, modeIndex);

    public void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors)
    {
        // OpenRgbClient.UpdateLeds sends synchronously, so a per-device buffer is safe to reuse.
        NativeColor[] native;
        lock (_nativeBuffers)
        {
            if (!_nativeBuffers.TryGetValue(deviceIndex, out native!) || native.Length != colors.Count)
            {
                native = new NativeColor[colors.Count];
                _nativeBuffers[deviceIndex] = native;
            }
        }

        for (var i = 0; i < colors.Count; i++)
        {
            native[i] = new NativeColor(colors[i].R, colors[i].G, colors[i].B);
        }

        RequireClient().UpdateLeds(deviceIndex, native);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }

    private OpenRGB.NET.OpenRgbClient RequireClient() =>
        _client ?? throw new InvalidOperationException("Not connected to OpenRGB. Call Connect() first.");

    private static EffectDeviceInfo ToEffectDevice(NativeDevice device)
    {
        var leds = new EffectLedInfo[device.Leds.Length];
        for (var i = 0; i < device.Leds.Length; i++)
        {
            var name = string.IsNullOrWhiteSpace(device.Leds[i].Name) ? $"LED {i}" : device.Leds[i].Name;
            leds[i] = new EffectLedInfo(i, name);
        }

        var zones = new EffectZoneInfo[device.Zones.Length];
        var ledOffset = 0;
        for (var i = 0; i < device.Zones.Length; i++)
        {
            zones[i] = ToEffectZone(device.Zones[i], ledOffset);
            ledOffset += (int)device.Zones[i].LedCount;
        }

        var modes = new EffectModeInfo[device.Modes.Length];
        for (var i = 0; i < device.Modes.Length; i++)
        {
            var mode = device.Modes[i];
            modes[i] = new EffectModeInfo(mode.Index, mode.Name, mode.ColorMode == NativeColorMode.PerLed);
        }

        return new EffectDeviceInfo(
            Index: device.Index,
            Name: device.Name,
            LedCount: device.Leds.Length,
            Leds: leds,
            Zones: zones,
            Modes: modes,
            ActiveModeIndex: device.ActiveModeIndex);
    }

    private static EffectZoneInfo ToEffectZone(NativeZone zone, int ledOffset)
    {
        var type = zone.Type switch
        {
            NativeZoneType.Single => EffectZoneType.SingleLed,
            NativeZoneType.Linear => EffectZoneType.Linear,
            NativeZoneType.Matrix => EffectZoneType.Matrix,
            _ => EffectZoneType.Linear,
        };

        int? width = null;
        int? height = null;
        int[]? matrixLedIndices = null;

        if (type == EffectZoneType.Matrix && zone.MatrixMap is { } matrixMap)
        {
            width = (int)matrixMap.Width;
            height = (int)matrixMap.Height;
            matrixLedIndices = new int[width.Value * height.Value];
            for (var row = 0; row < height.Value; row++)
            {
                for (var col = 0; col < width.Value; col++)
                {
                    var value = matrixMap.Matrix[row, col];
                    matrixLedIndices[(row * width.Value) + col] = value == uint.MaxValue ? -1 : (int)value;
                }
            }
        }

        return new EffectZoneInfo(
            Index: zone.Index,
            Name: zone.Name,
            Type: type,
            LedCount: (int)zone.LedCount,
            LedOffset: ledOffset,
            MatrixWidth: width,
            MatrixHeight: height,
            MatrixLedIndices: matrixLedIndices);
    }

}
