using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Lighting;

/// <inheritdoc cref="ILightingService"/>
public sealed class LightingService : ILightingService, IDisposable
{
    private static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromSeconds(3);

    /// <summary>How often <see cref="HeartbeatAsync"/> pings OpenRGB while connected, to notice a
    /// remote close that the underlying socket itself never surfaces as an error (OpenRGB.NET's
    /// read loop treats a 0-byte read as a normal end rather than throwing, and
    /// <c>Socket.Connected</c> stays true) - see docs/specs/05-lighting.md, acceptance #3.</summary>
    private static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(5);

    /// <summary>Extra time budget per device added to <see cref="_callTimeout"/> for
    /// <see cref="SetAllColorAsync"/>, so a large RGB setup doesn't get flagged as "timed out" just
    /// because it has many devices to walk through in one call.</summary>
    private const int PerDeviceTimeoutBudgetMs = 300;

    /// <summary>How long a timed-out call's orphaned action is given to actually unwind after the
    /// client is disposed (which unblocks OpenRGB.NET's blocking read), before this method returns
    /// control to the caller regardless.</summary>
    private static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromSeconds(1);

    private static readonly IReadOnlyList<RgbDevice> NoDevices = [];
    private static readonly IReadOnlyList<string> NoProfiles = [];

    private readonly IOpenRgbClient _client;
    private readonly ILogger<LightingService> _logger;
    private readonly TimeSpan _callTimeout;
    private readonly TimeSpan _heartbeatInterval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _isConnected;
    private Timer? _heartbeatTimer;
    private bool _disposed;

    /// <param name="callTimeout">
    /// How long a single call is allowed to take before it counts as "OpenRGB looks disconnected"
    /// (see <see cref="RunAsync{T}"/>). Defaults to 3 seconds (see 05-lighting.md); overridable only
    /// so tests can exercise the timeout path without a real multi-second wait.
    /// </param>
    /// <param name="heartbeatInterval">Overrides <see cref="DefaultHeartbeatInterval"/>; test seam
    /// only, same reasoning as <paramref name="callTimeout"/>.</param>
    /// <remarks>
    /// An earlier version of this class also disconnected in reaction to
    /// <c>IComponentService.StatusChanged</c> leaving <c>Running</c> for the openrgb component.
    /// That was removed: <c>ComponentService</c> only re-detects a component's status when asked
    /// (e.g. a page navigation, or an install/start action) - it does not itself notice OpenRGB's
    /// process exiting - so that reaction never actually fired for the "user closed OpenRGB" case
    /// it was meant to catch. The heartbeat below is the real detection path.
    /// </remarks>
    public LightingService(
        IOpenRgbClient client,
        ILogger<LightingService> logger,
        TimeSpan? callTimeout = null,
        TimeSpan? heartbeatInterval = null)
    {
        _client = client;
        _logger = logger;
        _callTimeout = callTimeout ?? DefaultCallTimeout;
        _heartbeatInterval = heartbeatInterval ?? DefaultHeartbeatInterval;

        _client.DeviceListUpdated += OnClientDeviceListUpdated;
    }

    public bool IsConnected => _isConnected;

    public event EventHandler? Disconnected;

    public event EventHandler? DevicesChanged;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        var connected = await RunAsync(
                () =>
                {
                    _client.Connect();
                    return _client.Connected;
                },
                defaultValue: false,
                what: "connect to OpenRGB",
                requiresConnection: false,
                cancellationToken)
            .ConfigureAwait(false);

        if (connected)
        {
            StartHeartbeat();
        }

        return connected;
    }

    public Task<IReadOnlyList<RgbDevice>> GetDevicesAsync(CancellationToken cancellationToken) =>
        RunAsync(
            () => _client.GetAllControllerData(),
            defaultValue: NoDevices,
            what: "list OpenRGB devices",
            requiresConnection: true,
            cancellationToken);

    public Task<bool> SetDeviceColorAsync(int deviceIndex, RgbColor color, CancellationToken cancellationToken) =>
        RunAsync(
            () =>
            {
                var device = FindDevice(deviceIndex);
                ApplyColor(device, color);
                return true;
            },
            defaultValue: false,
            what: "set a device color",
            requiresConnection: true,
            cancellationToken);

    public async Task<LightingApplyResult> SetAllColorAsync(RgbColor color, CancellationToken cancellationToken)
    {
        if (!_isConnected)
        {
            return LightingApplyResult.NotConnected;
        }

        // A quick, cheap round trip so the main call below gets a timeout budget that scales with
        // how many devices it actually has to walk through, instead of a single fixed timeout that
        // either wastes time on a small setup or falsely trips on a large one.
        var deviceCount = await RunAsync(
                () => _client.GetControllerCount(),
                defaultValue: 0,
                what: "count OpenRGB devices",
                requiresConnection: true,
                cancellationToken)
            .ConfigureAwait(false);

        var timeout = _callTimeout + TimeSpan.FromMilliseconds(Math.Max(deviceCount, 0) * PerDeviceTimeoutBudgetMs);

        return await RunAsync(
                () =>
                {
                    var succeeded = 0;
                    var failed = 0;
                    foreach (var device in _client.GetAllControllerData())
                    {
                        try
                        {
                            ApplyColor(device, color);
                            succeeded++;
                        }
                        catch (Exception ex) when (!IsConnectionException(ex))
                        {
                            // A single device's own quirk (an unusual mode, a report we don't
                            // handle) must not stop the rest of the room from being set - see
                            // docs/specs/05-lighting.md.
                            failed++;
                            _logger.LogWarning(ex, "Could not set color for device {Device}.", device.Name);
                        }
                    }

                    return new LightingApplyResult(succeeded, failed);
                },
                defaultValue: LightingApplyResult.NotConnected,
                what: "set all device colors",
                requiresConnection: true,
                cancellationToken,
                timeoutOverride: timeout)
            .ConfigureAwait(false);
    }

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

                SetModeIfNeeded(device, mode);
                return true;
            },
            defaultValue: false,
            what: "change a device's mode",
            requiresConnection: true,
            cancellationToken);

    public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken cancellationToken) =>
        RunAsync(
            () => _client.GetProfiles(),
            defaultValue: NoProfiles,
            what: "list OpenRGB profiles",
            requiresConnection: true,
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
            requiresConnection: true,
            cancellationToken);

    public Task<LightingApplyResult> TurnOffAllAsync(CancellationToken cancellationToken) =>
        SetAllColorAsync(RgbColor.Black, cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopHeartbeat();
        _client.DeviceListUpdated -= OnClientDeviceListUpdated;
        _client.Dispose();
        // The semaphore is intentionally not disposed: a call already past the WaitAsync() above
        // could still be mid-flight (e.g. its own finally about to run Release()) when Dispose()
        // runs on another thread; disposing it here would turn that Release() into an
        // ObjectDisposedException instead of a harmless no-op.
    }

    private RgbDevice FindDevice(int deviceIndex)
    {
        if (deviceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceIndex), deviceIndex, "Device index cannot be negative.");
        }

        return _client.GetControllerData(deviceIndex);
    }

    /// <summary>Switches the device to <paramref name="color"/>, selecting "Direct" or "Static"
    /// first when available (see <see cref="LightingModeSelector"/>). Skips
    /// <see cref="IOpenRgbClient.UpdateLeds"/> for a zero-LED device (OpenRGB.NET throws on an
    /// empty color array) and instead sends the color through the mode itself when that mode is
    /// <see cref="RgbColorMode.ModeSpecific"/> - what OpenRGB's own UI does for a GPU or RAM stick
    /// whose only color mode is "Static".</summary>
    private void ApplyColor(RgbDevice device, RgbColor color)
    {
        var mode = LightingModeSelector.SelectColorMode(device.Modes);
        if (mode is not null)
        {
            if (mode.ColorMode == RgbColorMode.ModeSpecific && mode.ColorCount > 0)
            {
                var modeColors = CreateColorArray(color, mode.ColorCount);
                _client.SetMode(device.Index, mode.Index, modeColors);
            }
            else
            {
                SetModeIfNeeded(device, mode);
            }
        }

        if (device.LedCount > 0)
        {
            _client.UpdateLeds(device.Index, CreateColorArray(color, device.LedCount));
        }
    }

    private void SetModeIfNeeded(RgbDevice device, RgbMode mode)
    {
        if (string.Equals(device.ActiveMode, mode.Name, StringComparison.OrdinalIgnoreCase))
        {
            // Already on this mode; skip the round trip.
            return;
        }

        _client.SetMode(device.Index, mode.Index);
    }

    private static RgbColor[] CreateColorArray(RgbColor color, int count)
    {
        var colors = new RgbColor[Math.Max(count, 0)];
        Array.Fill(colors, color);
        return colors;
    }

    private static bool IsConnectionException(Exception ex) =>
        ex is SocketException or IOException or TimeoutException or ObjectDisposedException;

    private void StartHeartbeat() =>
        _heartbeatTimer ??= new Timer(_ => FireAndForgetHeartbeat(), null, _heartbeatInterval, _heartbeatInterval);

    private void StopHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }

    private void FireAndForgetHeartbeat() => _ = HeartbeatAsync();

    /// <summary>Pings OpenRGB (see <see cref="DefaultHeartbeatInterval"/>) so a remote close that
    /// never throws (see the class doc) is still noticed within one interval plus one call
    /// timeout, instead of only on the next user-triggered call.</summary>
    private async Task HeartbeatAsync()
    {
        if (!_isConnected)
        {
            return;
        }

        await RunAsync(
                () => _client.GetControllerCount(),
                defaultValue: -1,
                what: "check the OpenRGB heartbeat",
                requiresConnection: true,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <paramref name="action"/> off the calling thread, serialized against every other call
    /// through <see cref="_gate"/> (OpenRGB.NET's client is not thread-safe), bounded by
    /// <paramref name="timeoutOverride"/> or <see cref="_callTimeout"/>. Never lets an exception
    /// (other than cancellation) escape: a timeout, a connection-related failure, or (if
    /// <see cref="_client"/> itself reports not connected) any other failure from
    /// <paramref name="action"/> is logged, flips <see cref="IsConnected"/> to false, raises
    /// <see cref="Disconnected"/>, and this returns <paramref name="defaultValue"/> instead. A
    /// non-connection failure (e.g. an invalid argument from the caller) is logged and returns the
    /// default too, but does not disconnect - see docs/specs/05-lighting.md.
    /// </summary>
    /// <remarks>
    /// On a timeout - and on cancellation, since by then <paramref name="action"/> is already
    /// running unsupervised on a pool thread against a client that isn't thread-safe - the client is
    /// disposed <em>while still holding the gate</em>. OpenRGB.NET's own read has no timeout of its
    /// own (it blocks on <c>BlockingCollection.Take</c>), so disposing is what actually unblocks the
    /// orphaned call; without it, the orphan could still be reading from the socket when the next
    /// call starts and steal its reply. The gate is only released after giving the orphan a short
    /// grace period to unwind post-dispose.
    /// </remarks>
    private async Task<T> RunAsync<T>(
        Func<T> action,
        T defaultValue,
        string what,
        bool requiresConnection,
        CancellationToken cancellationToken,
        TimeSpan? timeoutOverride = null)
    {
        if (requiresConnection && !_isConnected)
        {
            // Fail fast without touching the client at all - it may already be disposed (see the
            // timeout path below) and there is nothing useful to attempt until ConnectAsync runs.
            return defaultValue;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var timeout = timeoutOverride ?? _callTimeout;
            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var work = Task.Run(action, cancellationToken);
            var delay = Task.Delay(timeout, delayCts.Token);

            var finished = await Task.WhenAny(work, delay).ConfigureAwait(false);
            if (finished == delay)
            {
                var wasCancelled = cancellationToken.IsCancellationRequested;

                DisposeClientSafely();
                await Task.WhenAny(work, Task.Delay(OrphanGracePeriod)).ConfigureAwait(false);
                HandleDisconnect();

                if (wasCancelled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                _logger.LogWarning("Timed out trying to {What}; disconnected OpenRGB to recover.", what);
                return defaultValue;
            }

            // Work finished first; stop the still-pending delay timer instead of leaking it.
            await delayCts.CancelAsync().ConfigureAwait(false);

            var result = await work.ConfigureAwait(false);

            if (TryCheckClientConnected())
            {
                _isConnected = true;
                return result;
            }

            // The action itself did not throw, but the client no longer considers itself
            // connected - e.g. a phantom reply from a since-fixed vendored bug, or simply
            // Connect() itself returning false. Never assign IsConnected directly: always go
            // through the same dispose-and-announce path as every other kind of disconnect, so a
            // real disconnect is never silently swallowed with no event, no log, and no cleanup
            // (see docs/upstream/openrgb-net.md for the bug that originally masked this).
            DisposeClientSafely();
            HandleDisconnect();
            return defaultValue;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (IsConnectionException(ex) || !TryCheckClientConnected())
            {
                _logger.LogWarning(ex, "Could not {What}; treating OpenRGB as disconnected.", what);
                HandleDisconnect();
            }
            else
            {
                _logger.LogWarning(ex, "Could not {What}.", what);
            }

            return defaultValue;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Reads <see cref="IOpenRgbClient.Connected"/> defensively - this runs from inside an
    /// exception handler, and the client may already be in a bad state.</summary>
    private bool TryCheckClientConnected()
    {
        try
        {
            return _client.Connected;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the OpenRGB client's own Connected state.");
            return false;
        }
    }

    private void DisposeClientSafely()
    {
        try
        {
            _client.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring failure disposing the OpenRGB client during recovery.");
        }
    }

    private void HandleDisconnect()
    {
        var wasConnected = _isConnected;
        _isConnected = false;
        StopHeartbeat();

        if (wasConnected)
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnClientDeviceListUpdated(object? sender, EventArgs e) => DevicesChanged?.Invoke(this, EventArgs.Empty);
}
