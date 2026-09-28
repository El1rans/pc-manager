using NativeColor = OpenRGB.NET.Color;
using NativeColorMode = OpenRGB.NET.ColorMode;
using NativeDevice = OpenRGB.NET.Device;
using NativeDeviceType = OpenRGB.NET.DeviceType;
using NativeZoneType = OpenRGB.NET.ZoneType;

namespace Porchlight.Core.Lighting;

/// <inheritdoc cref="IOpenRgbClient"/>
public sealed class OpenRgbClientAdapter : IOpenRgbClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;
    private readonly Lock _lock = new();
    private OpenRGB.NET.OpenRgbClient? _client;

    /// <summary>The client a <see cref="Connect"/> call currently in flight has constructed, if
    /// any - stored here (before the potentially slow/blocking handshake in
    /// <c>OpenRGB.NET.OpenRgbClient.Connect</c> even starts) purely so a concurrent
    /// <see cref="Dispose"/> (e.g. <c>LightingService</c> recovering from a timeout on another
    /// thread) has something to dispose. Disposing it there is what actually unblocks that
    /// in-flight handshake, instead of leaking its socket and leaving that thread stuck forever.
    /// </summary>
    private OpenRGB.NET.OpenRgbClient? _pendingClient;

    /// <summary>Incremented on every <see cref="Connect"/> call and every <see cref="Dispose"/>;
    /// lets a <see cref="Connect"/> that was superseded while it was still connecting (by a
    /// Dispose()+reconnect that ran on another thread) recognize that and discard its own,
    /// now-stale result instead of overwriting a newer <see cref="_client"/>.</summary>
    private long _generation;

    public OpenRgbClientAdapter(string host, int port, int timeoutMs)
    {
        _host = host;
        _port = port;
        _timeoutMs = timeoutMs;
    }

    public bool Connected => _client?.Connected == true;

    public event EventHandler? DeviceListUpdated;

    public void Connect()
    {
        if (Connected)
        {
            // OpenRGB.NET's client has no reconnect of its own once its socket has died, but it
            // also has no way to ask "is this actually still good" beyond the Connected flag, so a
            // client that reports itself connected is left alone rather than torn down and rebuilt
            // on every call.
            return;
        }

        long generation;
        lock (_lock)
        {
            DisposeClientLocked();
            generation = ++_generation;
        }

        // autoConnect: false so a failed Connect() below leaves us able to dispose the half-built
        // client instead of leaking its socket (the constructor's own auto-connect gives no chance
        // to clean up before the exception reaches the caller).
        var client = new OpenRGB.NET.OpenRgbClient(
            ip: _host, port: _port, name: "Porchlight", autoConnect: false, timeoutMs: _timeoutMs);

        lock (_lock)
        {
            if (generation != _generation)
            {
                // Superseded (another Connect()/Dispose() already ran) before the handshake even
                // started - discard immediately.
                client.Dispose();
                return;
            }

            _pendingClient = client;
        }

        try
        {
            client.Connect();
        }
        catch
        {
            lock (_lock)
            {
                if (generation == _generation)
                {
                    // Still the current attempt - nobody superseded it, so it wasn't already
                    // disposed by a concurrent Connect()/Dispose()'s own DisposeClientLocked();
                    // clean it up ourselves. (If generation changed, that call already disposed
                    // this exact client - disposing it again here would throw.)
                    _pendingClient = null;
                    client.Dispose();
                }
            }

            throw;
        }

        lock (_lock)
        {
            if (generation != _generation)
            {
                // Superseded while the handshake was in flight (a concurrent Dispose(), or a whole
                // new Connect()) - already disposed by that call's own DisposeClientLocked(), so
                // there is nothing left to clean up; just don't let this stale success overwrite a
                // newer _client.
                return;
            }

            client.DeviceListUpdated += OnNativeDeviceListUpdated;
            _pendingClient = null;
            _client = client;
        }
    }

    public int GetControllerCount() => RequireClient().GetControllerCount();

    public RgbDevice GetControllerData(int deviceIndex) => ToRgbDevice(RequireClient().GetControllerData(deviceIndex));

    public IReadOnlyList<RgbDevice> GetAllControllerData() =>
        Array.ConvertAll(RequireClient().GetAllControllerData(), ToRgbDevice);

    public IReadOnlyList<string> GetProfiles() => RequireClient().GetProfiles();

    public void LoadProfile(string name) => RequireClient().LoadProfile(name);

    public void SetMode(int deviceIndex, int modeIndex, IReadOnlyList<RgbColor>? colors = null) =>
        RequireClient().UpdateMode(deviceIndex, modeIndex, colors: colors is null ? null : ToNativeColors(colors));

    public void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors) =>
        RequireClient().UpdateLeds(deviceIndex, ToNativeColors(colors));

    public void Dispose()
    {
        lock (_lock)
        {
            // Bump the generation first so an in-flight Connect() (see above) recognizes, once its
            // handshake returns (or throws), that it has been superseded and must not resurrect
            // _client or double-dispose whatever DisposeClientLocked() below is about to clean up.
            _generation++;
            DisposeClientLocked();
        }
    }

    /// <summary>Disposes both <see cref="_client"/> (the last established connection) and
    /// <see cref="_pendingClient"/> (an in-flight <see cref="Connect"/> call's not-yet-established
    /// one, if any - disposing it is what unblocks that call's blocking handshake). Callers hold
    /// <see cref="_lock"/>.</summary>
    private void DisposeClientLocked()
    {
        if (_client is not null)
        {
            _client.DeviceListUpdated -= OnNativeDeviceListUpdated;
            _client.Dispose();
            _client = null;
        }

        if (_pendingClient is not null)
        {
            _pendingClient.Dispose();
            _pendingClient = null;
        }
    }

    private void OnNativeDeviceListUpdated(object? sender, EventArgs e) => DeviceListUpdated?.Invoke(this, EventArgs.Empty);

    private OpenRGB.NET.OpenRgbClient RequireClient() =>
        _client ?? throw new InvalidOperationException("Not connected to OpenRGB. Call Connect() first.");

    private static NativeColor[] ToNativeColors(IReadOnlyList<RgbColor> colors)
    {
        var native = new NativeColor[colors.Count];
        for (var i = 0; i < colors.Count; i++)
        {
            native[i] = new NativeColor(colors[i].R, colors[i].G, colors[i].B);
        }

        return native;
    }

    private static RgbDevice ToRgbDevice(NativeDevice device)
    {
        var modes = Array.ConvertAll(device.Modes, ToRgbMode);
        var zones = Array.ConvertAll(device.Zones, z => new RgbZone(z.Name, (int)z.LedCount, z.Type == NativeZoneType.Matrix));

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

    private static RgbMode ToRgbMode(OpenRGB.NET.Mode mode) =>
        new(mode.Index, mode.Name, ToRgbColorMode(mode.ColorMode), mode.Colors.Length);

    private static RgbColorMode ToRgbColorMode(NativeColorMode colorMode) => colorMode switch
    {
        NativeColorMode.PerLed => RgbColorMode.PerLed,
        NativeColorMode.ModeSpecific => RgbColorMode.ModeSpecific,
        NativeColorMode.Random => RgbColorMode.Random,
        _ => RgbColorMode.None,
    };

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
