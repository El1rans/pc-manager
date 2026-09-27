using Microsoft.Extensions.Logging;

namespace PCManager.Core.Lighting;

/// <inheritdoc cref="ILightingService"/>
public sealed class LightingService : ILightingService, IDisposable
{
    private static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromSeconds(3);
    private static readonly IReadOnlyList<RgbDevice> NoDevices = [];
    private static readonly IReadOnlyList<string> NoProfiles = [];

    private readonly IOpenRgbClient _client;
    private readonly ILogger<LightingService> _logger;
    private readonly TimeSpan _callTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _isConnected;

    /// <param name="callTimeout">
    /// How long a single call is allowed to take before it counts as "OpenRGB looks disconnected"
    /// (see <see cref="RunAsync{T}"/>). Defaults to 3 seconds (see 05-lighting.md); overridable only
    /// so tests can exercise the timeout path without a real multi-second wait.
    /// </param>
    public LightingService(IOpenRgbClient client, ILogger<LightingService> logger, TimeSpan? callTimeout = null)
    {
        _client = client;
        _logger = logger;
        _callTimeout = callTimeout ?? DefaultCallTimeout;
    }

    public bool IsConnected => _isConnected;

    public event EventHandler? Disconnected;

    public Task<bool> ConnectAsync(CancellationToken cancellationToken) =>
        RunAsync(
            () =>
            {
                _client.Connect();
                return _client.Connected;
            },
            defaultValue: false,
            what: "connect to OpenRGB",
            cancellationToken);

    public Task<IReadOnlyList<RgbDevice>> GetDevicesAsync(CancellationToken cancellationToken) =>
        RunAsync(
            () => _client.GetAllControllerData(),
            defaultValue: NoDevices,
            what: "list OpenRGB devices",
            cancellationToken);

    public Task<bool> SetDeviceColorAsync(int deviceIndex, RgbColor color, CancellationToken cancellationToken) =>
        RunAsync(
            () =>
            {
                var device = FindDevice(deviceIndex);
                SelectColorModeIfAvailable(device);
                _client.UpdateLeds(device.Index, CreateColorArray(color, device.LedCount));
                return true;
            },
            defaultValue: false,
            what: "set a device color",
            cancellationToken);

    public Task<bool> SetAllColorAsync(RgbColor color, CancellationToken cancellationToken) =>
        RunAsync(
            () =>
            {
                foreach (var device in _client.GetAllControllerData())
                {
                    SelectColorModeIfAvailable(device);
                    _client.UpdateLeds(device.Index, CreateColorArray(color, device.LedCount));
                }

                return true;
            },
            defaultValue: false,
            what: "set all device colors",
            cancellationToken);

    public Task<bool> SetModeAsync(int deviceIndex, string modeName, CancellationToken cancellationToken) =>
        RunAsync(
            () =>
            {
                var device = FindDevice(deviceIndex);
                var mode = device.Modes.FirstOrDefault(
                    m => string.Equals(m.Name, modeName, StringComparison.OrdinalIgnoreCase));
                if (mode is null)
                {
                    throw new InvalidOperationException($"Device {deviceIndex} has no mode named '{modeName}'.");
                }

                _client.SetMode(device.Index, mode.Index);
                return true;
            },
            defaultValue: false,
            what: "change a device's mode",
            cancellationToken);

    public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken cancellationToken) =>
        RunAsync(
            () => _client.GetProfiles(),
            defaultValue: NoProfiles,
            what: "list OpenRGB profiles",
            cancellationToken);

    public Task<bool> LoadProfileAsync(string name, CancellationToken cancellationToken) =>
        RunAsync(
            () =>
            {
                _client.LoadProfile(name);
                return true;
            },
            defaultValue: false,
            what: "load an OpenRGB profile",
            cancellationToken);

    public Task<bool> TurnOffAllAsync(CancellationToken cancellationToken) =>
        SetAllColorAsync(RgbColor.Black, cancellationToken);

    public void Dispose()
    {
        _gate.Dispose();
        _client.Dispose();
    }

    private RgbDevice FindDevice(int deviceIndex) =>
        _client.GetAllControllerData().FirstOrDefault(d => d.Index == deviceIndex)
        ?? throw new InvalidOperationException($"No OpenRGB device with index {deviceIndex}.");

    private void SelectColorModeIfAvailable(RgbDevice device)
    {
        var mode = LightingModeSelector.SelectColorMode(device.Modes);
        if (mode is not null)
        {
            _client.SetMode(device.Index, mode.Index);
        }
    }

    private static RgbColor[] CreateColorArray(RgbColor color, int count)
    {
        var colors = new RgbColor[Math.Max(count, 0)];
        Array.Fill(colors, color);
        return colors;
    }

    /// <summary>
    /// Runs <paramref name="action"/> off the calling thread, serialized against every other call
    /// through <see cref="_gate"/> (OpenRGB.NET's client is not thread-safe), bounded by
    /// <see cref="CallTimeout"/>. Never lets an exception (other than cancellation) escape: a
    /// timeout or any failure from <paramref name="action"/> is logged, flips
    /// <see cref="IsConnected"/> to false, raises <see cref="Disconnected"/>, and this returns
    /// <paramref name="defaultValue"/> instead.
    /// </summary>
    private async Task<T> RunAsync<T>(
        Func<T> action, T defaultValue, string what, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var work = Task.Run(action, cancellationToken);
            var delay = Task.Delay(_callTimeout, delayCts.Token);

            var finished = await Task.WhenAny(work, delay).ConfigureAwait(false);
            if (finished == delay)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogWarning("Timed out trying to {What}.", what);
                HandleDisconnect();
                return defaultValue;
            }

            // Work finished first; stop the still-pending delay timer instead of leaking it.
            await delayCts.CancelAsync().ConfigureAwait(false);

            var result = await work.ConfigureAwait(false);
            _isConnected = _client.Connected;
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not {What}; treating OpenRGB as disconnected.", what);
            HandleDisconnect();
            return defaultValue;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void HandleDisconnect()
    {
        _isConnected = false;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }
}
