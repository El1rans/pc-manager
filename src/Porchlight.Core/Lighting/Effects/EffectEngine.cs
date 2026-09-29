using Microsoft.Extensions.Logging;
using Porchlight.Core.Hardware;

namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// Runs the frame loop that renders each assigned device's <see cref="IEffect"/> and sends it to
/// OpenRGB - see docs/specs/11-led-effects.md. Owns its own <see cref="IEffectDeviceClient"/>
/// connection; a single instance is registered as a DI singleton (see
/// <see cref="LedEffectsServiceCollectionExtensions"/>) and is not started automatically - a caller
/// (the phase 2 Lighting UI) calls <see cref="Start"/>/<see cref="Stop"/> explicitly.
/// </summary>
public sealed class EffectEngine : IDisposable
{
    public const int MinFps = 10;
    public const int MaxFps = 60;
    public const int DefaultFps = 30;

    /// <summary>How long a key press stays in <see cref="IEffectContext.RecentKeyPresses"/> before
    /// being dropped, regardless of any individual effect's own max-age setting.</summary>
    private static readonly TimeSpan KeyPressRetention = TimeSpan.FromSeconds(3);

    private readonly IEffectDeviceClient _client;
    private readonly IHardwareService _hardwareService;
    private readonly IPendingUpdateCountProvider _pendingUpdateCountProvider;
    private readonly IDeviceExclusionProvider _exclusionProvider;
    private readonly IKeyPressSource _keyPressSource;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EffectEngine> _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, EffectAssignment> _assignments = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<KeyPressEvent> _recentKeyPresses = [];
    private readonly List<RunningDevice> _activeDevices = [];

    private RunningDevice[] _deviceSnapshot = [];
    private int _tickInProgress;
    private bool _cpuTemperatureFailing;
    private bool _pendingUpdateCountFailing;

    private ITimer? _timer;
    private long _startTimestamp;
    private bool _running;
    private bool _disposed;

    public EffectEngine(
        IEffectDeviceClient client,
        IHardwareService hardwareService,
        IPendingUpdateCountProvider pendingUpdateCountProvider,
        IDeviceExclusionProvider exclusionProvider,
        IKeyPressSource keyPressSource,
        ILogger<EffectEngine> logger,
        TimeProvider? timeProvider = null)
    {
        _client = client;
        _hardwareService = hardwareService;
        _pendingUpdateCountProvider = pendingUpdateCountProvider;
        _exclusionProvider = exclusionProvider;
        _keyPressSource = keyPressSource;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Whether the frame loop is currently running.</summary>
    public bool IsRunning
    {
        get { lock (_gate) { return _running; } }
    }

    /// <summary>Names of devices currently being rendered to (assigned, not excluded, connected,
    /// with at least one LED in their chosen zone).</summary>
    public IReadOnlyList<string> RunningDeviceNames
    {
        get { lock (_gate) { return _activeDevices.Where(d => !d.Paused).Select(d => d.DeviceName).ToList(); } }
    }

    /// <summary>Names of devices the engine stopped rendering to after their effect threw - see
    /// the class remarks on <see cref="OnTick"/>.</summary>
    public IReadOnlyList<string> PausedDeviceNames
    {
        get { lock (_gate) { return _activeDevices.Where(d => d.Paused).Select(d => d.DeviceName).ToList(); } }
    }

    /// <summary>Replaces every device -> effect assignment. Safe to call before or while running;
    /// takes effect on the next <see cref="Start"/> (not applied to an already-running engine - call
    /// <see cref="Stop"/> then <see cref="Start"/> to pick up a change immediately).</summary>
    public void SetAssignments(IEnumerable<EffectAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        lock (_gate)
        {
            _assignments.Clear();
            foreach (var assignment in assignments)
            {
                if (!string.IsNullOrWhiteSpace(assignment.DeviceKey))
                {
                    _assignments[assignment.DeviceKey] = assignment;
                }
            }
        }
    }

    /// <summary>
    /// Connects to OpenRGB, switches every assigned, non-excluded device to its "Direct" mode
    /// (once), and starts the frame loop at <paramref name="fps"/> (clamped to
    /// <see cref="MinFps"/>..<see cref="MaxFps"/>). A no-op if already running. Never throws - a
    /// connection failure is logged and leaves the engine not running (as if never
    /// started).
    /// </summary>
    public void Start(int fps = DefaultFps)
    {
        lock (_gate)
        {
            if (_running)
            {
                return;
            }

            var clampedFps = Math.Clamp(fps, MinFps, MaxFps);

            try
            {
                _client.Connect();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not connect to OpenRGB for LED effects.");
                return;
            }

            if (!_client.Connected)
            {
                _logger.LogWarning("Not connected to OpenRGB; LED effects engine did not start.");
                return;
            }

            _activeDevices.Clear();
            foreach (var device in _client.GetAllDevices())
            {
                var running = TryStartDevice(device);
                if (running is not null)
                {
                    _activeDevices.Add(running);
                }
            }

            _deviceSnapshot = [.. _activeDevices];

            _recentKeyPresses.Clear();
            _keyPressSource.KeyPressed += OnKeyPressed;

            _startTimestamp = _timeProvider.GetTimestamp();
            var period = TimeSpan.FromSeconds(1.0 / clampedFps);
            _timer = _timeProvider.CreateTimer(OnTick, null, period, period);
            _running = true;
        }
    }

    /// <summary>Stops the frame loop and restores every device it switched modes on back to
    /// whatever mode was active before <see cref="Start"/>. A no-op if not running. Errors
    /// restoring an individual device are logged and do not stop the rest.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (!_running)
            {
                return;
            }

            _timer?.Dispose();
            _timer = null;
            _keyPressSource.KeyPressed -= OnKeyPressed;

            foreach (var device in _activeDevices)
            {
                RestoreMode(device);
            }

            _activeDevices.Clear();
            _deviceSnapshot = [];
            _running = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _client.Dispose();
    }

    private RunningDevice? TryStartDevice(EffectDeviceInfo device)
    {
        if (!_assignments.TryGetValue(device.Name, out var assignment))
        {
            return null;
        }

        if (_exclusionProvider.IsExcluded(device.Name))
        {
            return null;
        }

        var effect = EffectRegistry.CreateWithOverlay(assignment.EffectName, assignment.Settings, assignment.ShowUpdatesAlert);
        if (effect is null)
        {
            _logger.LogWarning(
                "Device {Device} is assigned unknown effect '{Effect}'; skipping.", device.Name, assignment.EffectName);
            return null;
        }

        var zone = SelectPrimaryZone(device.Zones);
        var layout = zone is null ? LedLayout.Empty : LedLayoutBuilder.Build(zone, device.Leds);
        if (layout.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Device {Device} has no LEDs to render effects onto; skipping.", device.Name);
            }

            return null;
        }

        var directMode = EffectModeSelector.SelectDirectMode(device.Modes);
        if (directMode is not null && directMode.Index != device.ActiveModeIndex)
        {
            try
            {
                _client.SetMode(device.Index, directMode.Index);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not switch device {Device} to Direct mode; skipping.", device.Name);
                return null;
            }
        }

        return new RunningDevice(device.Index, device.Name, device.LedCount, effect, layout, device.ActiveModeIndex);
    }

    private void RestoreMode(RunningDevice device)
    {
        try
        {
            _client.SetMode(device.DeviceIndex, device.OriginalModeIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore device {Device}'s previous mode.", device.DeviceName);
        }
    }

    /// <summary>
    /// One frame tick: builds a fresh <see cref="IEffectContext"/>, renders every active device's
    /// effect, and sends it to OpenRGB only if it changed since the last send (per device). A
    /// device whose effect throws is logged and marked "paused" - it stops being rendered to (and
    /// stops being sent anything further) for the rest of this run, but every other device keeps
    /// going.
    /// </summary>
    private void OnTick(object? state)
    {
        // The timer fires on the thread pool regardless of whether the previous tick has finished,
        // and UpdateLeds blocks on TCP - skip this tick rather than pile up (and race on the
        // per-device buffers).
        if (Interlocked.Exchange(ref _tickInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            RunningDevice[] devices;
            TimeSpan elapsed;
            IEffectContext context;

            lock (_gate)
            {
                if (!_running)
                {
                    return;
                }

                elapsed = _timeProvider.GetElapsedTime(_startTimestamp);
                TrimOldKeyPresses();
                context = BuildContext();
                devices = _deviceSnapshot;
            }

            foreach (var device in devices)
            {
                if (device.Paused)
                {
                    continue;
                }

                RenderAndSend(device, elapsed, context);
            }
        }
        finally
        {
            Volatile.Write(ref _tickInProgress, 0);
        }
    }

    private void RenderAndSend(RunningDevice device, TimeSpan elapsed, IEffectContext context)
    {
        try
        {
            // Double-buffered: this frame renders into the spare buffer, which becomes LastSent (and
            // the old LastSent the next spare) only once actually sent. Cleared first since effects
            // may rely on a black default.
            var buffer = device.Scratch ?? new RgbColor[device.Layout.Count];
            Array.Clear(buffer);
            var frame = new EffectFrame(elapsed, device.Layout, context);
            device.Effect.Render(in frame, buffer);

            if (device.LastSent is not null && buffer.AsSpan().SequenceEqual(device.LastSent))
            {
                return;
            }

            // UpdateLeds is synchronous and must not retain the list (see IEffectDeviceClient), so
            // one full-size buffer per device is reused every send.
            var fullBuffer = device.FullBuffer ??= new RgbColor[device.LedCount];
            Array.Clear(fullBuffer);
            for (var i = 0; i < device.Layout.Points.Count; i++)
            {
                var ledIndex = device.Layout.Points[i].DeviceLedIndex;
                if (ledIndex >= 0 && ledIndex < fullBuffer.Length)
                {
                    fullBuffer[ledIndex] = buffer[i];
                }
            }

            _client.UpdateLeds(device.DeviceIndex, fullBuffer);
            device.Scratch = device.LastSent;
            device.LastSent = buffer;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LED effect for device {Device} failed; pausing it.", device.DeviceName);
            device.Paused = true;
        }
    }

    private EffectContext BuildContext()
    {
        var utcNow = _timeProvider.GetUtcNow();
        double? cpuTemperature = null;

        // The two reads below run every frame, so each logs only the first failure of a streak
        // (and one line on recovery) instead of once per frame.
        try
        {
            cpuTemperature = CpuTemperatureReader.Read(_hardwareService.Latest);
            NoteRecovered(ref _cpuTemperatureFailing, "CPU temperature");
        }
        catch (Exception ex)
        {
            NoteFailed(ref _cpuTemperatureFailing, ex, "CPU temperature");
        }

        var pendingUpdateCount = 0;
        try
        {
            pendingUpdateCount = _pendingUpdateCountProvider.GetPendingUpdateCount();
            NoteRecovered(ref _pendingUpdateCountFailing, "pending update count");
        }
        catch (Exception ex)
        {
            NoteFailed(ref _pendingUpdateCountFailing, ex, "pending update count");
        }

        IReadOnlyList<KeyPressEvent> keyPresses = _recentKeyPresses.Count == 0 ? [] : [.. _recentKeyPresses];
        return new EffectContext(utcNow, cpuTemperature, pendingUpdateCount, keyPresses);
    }

    private void NoteFailed(ref bool failing, Exception ex, string what)
    {
        if (!failing)
        {
            failing = true;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Could not read {What} for LED effects.", what);
            }
        }
    }

    private void NoteRecovered(ref bool failing, string what)
    {
        if (failing)
        {
            failing = false;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Reading {What} for LED effects recovered.", what);
            }
        }
    }

    private void OnKeyPressed(object? sender, KeyPressEvent e)
    {
        lock (_gate)
        {
            _recentKeyPresses.Add(e);
        }
    }

    /// <summary>Callers hold <see cref="_gate"/>.</summary>
    private void TrimOldKeyPresses()
    {
        var cutoff = _timeProvider.GetUtcNow() - KeyPressRetention;
        _recentKeyPresses.RemoveAll(k => k.Timestamp < cutoff);
    }

    /// <summary>Picks which zone to render onto: the matrix zone with the most LEDs if the device
    /// has one, else whichever non-empty zone has the most LEDs. Other zones on the same device
    /// (e.g. a second, smaller strip) are left off - a known phase 1 limitation, see
    /// docs/specs/11-led-effects.md.</summary>
    private static EffectZoneInfo? SelectPrimaryZone(IReadOnlyList<EffectZoneInfo> zones)
    {
        EffectZoneInfo? best = null;
        foreach (var zone in zones)
        {
            if (zone.LedCount <= 0)
            {
                continue;
            }

            if (best is null)
            {
                best = zone;
                continue;
            }

            var zoneIsBetter =
                (zone.Type == EffectZoneType.Matrix && best.Type != EffectZoneType.Matrix) ||
                (zone.Type == best.Type && zone.LedCount > best.LedCount);

            if (zoneIsBetter)
            {
                best = zone;
            }
        }

        return best;
    }

    private sealed class RunningDevice(
        int deviceIndex, string deviceName, int ledCount, IEffect effect, LedLayout layout, int originalModeIndex)
    {
        public int DeviceIndex { get; } = deviceIndex;

        public string DeviceName { get; } = deviceName;

        public int LedCount { get; } = ledCount;

        public IEffect Effect { get; } = effect;

        public LedLayout Layout { get; } = layout;

        public int OriginalModeIndex { get; } = originalModeIndex;

        public RgbColor[]? LastSent { get; set; }

        /// <summary>Spare frame buffer swapped with <see cref="LastSent"/> after each send.</summary>
        public RgbColor[]? Scratch { get; set; }

        /// <summary>Reused device-wide (all LEDs) buffer handed to the client on each send.</summary>
        public RgbColor[]? FullBuffer { get; set; }

        public bool Paused { get; set; }
    }
}
