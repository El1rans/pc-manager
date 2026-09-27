using Microsoft.Extensions.Logging;
using PCManager.Core.Settings;

namespace PCManager.Core.Hardware;

/// <summary>
/// Wires <see cref="FanControlEngine"/> (pure decisions) to real fans and settings: on every
/// <see cref="IHardwareService.SnapshotUpdated"/> tick it builds the engine's input from the
/// current settings and temperatures, evaluates, and applies the result to
/// <see cref="IHardwareService.Controllers"/> - all synchronously, on the hardware service's own
/// dedicated thread, so every <see cref="IFanController"/> call stays on the one thread that owns
/// the hardware library (spec 04). Every other entry point (<see cref="Activate"/>,
/// <see cref="Rearm"/>, <see cref="RestoreAll"/>, <see cref="Suspend"/>,
/// <see cref="ResumeFromSuspend"/>) is marshalled onto that same thread via
/// <see cref="IHardwareService.RunOnOwnerThread"/> so nothing ever races the tick.
/// </summary>
public sealed class FanControlManager : IDisposable
{
    /// <summary>How long an external call (restore on exit/suspend, re-arm) waits for the hardware
    /// thread before falling back to a direct call - see <see cref="IHardwareService.RunOnOwnerThread"/>.</summary>
    private static readonly TimeSpan OwnerThreadTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Rule 3/S3: a fan whose actual duty cycle stays more than this many percentage
    /// points off its commanded target for <see cref="ReadBackFailureStreak"/> consecutive ticks is
    /// treated as a silent set failure (some SuperIO backends return success from a failed write
    /// when their ISA bus mutex could not be acquired in time).</summary>
    private const double ReadBackToleranceLevel = 5;

    private const int ReadBackFailureStreak = 3;

    /// <summary>Independent watchdog default: if no snapshot has been successfully processed for
    /// this long, the hardware thread itself may be stuck or dead, so this fires without depending
    /// on it. Overridable by the internal test constructor only.</summary>
    private static readonly TimeSpan DefaultWatchdogTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan DefaultWatchdogPollInterval = TimeSpan.FromSeconds(1);

    private readonly IHardwareService _hardwareService;
    private readonly ISettingsStore _settingsStore;
    private readonly FanControlEngine _engine;
    private readonly IFanControlActivityMarker _activityMarker;
    private readonly ILogger<FanControlManager> _logger;
    private readonly Lock _stateLock = new();
    private readonly Dictionary<string, PendingCheck> _pendingReadBacks = [];
    private readonly Timer _watchdogTimer;
    private readonly TimeSpan _watchdogTimeout;

    private bool _armed;
    private bool _paused;
    private bool _failureAlertRaised;
    private bool _noTempFailsafeActive;
    private bool _forceRewriteAfterResume;
    private bool _activityMarkerActive;
    private long _lastSnapshotProcessedUtcTicks = DateTime.UtcNow.Ticks;
    private bool _watchdogTripped;

    public FanControlManager(
        IHardwareService hardwareService,
        ISettingsStore settingsStore,
        FanControlEngine engine,
        IFanControlActivityMarker activityMarker,
        ILogger<FanControlManager> logger)
        : this(hardwareService, settingsStore, engine, activityMarker, logger, DefaultWatchdogTimeout, DefaultWatchdogPollInterval)
    {
    }

    /// <summary>Test seam: lets tests use a short watchdog timeout/poll interval instead of waiting
    /// out the real 5-second default.</summary>
    internal FanControlManager(
        IHardwareService hardwareService,
        ISettingsStore settingsStore,
        FanControlEngine engine,
        IFanControlActivityMarker activityMarker,
        ILogger<FanControlManager> logger,
        TimeSpan watchdogTimeout,
        TimeSpan watchdogPollInterval)
    {
        _hardwareService = hardwareService;
        _settingsStore = settingsStore;
        _engine = engine;
        _activityMarker = activityMarker;
        _logger = logger;
        _watchdogTimeout = watchdogTimeout;

        // S8: a marker still present at construction means the previous session ended without
        // going through any restore path (forced kill, crash, BSOD, power loss) - surface it once,
        // then clear it so the warning does not repeat on every future launch.
        if (_activityMarker.Exists())
        {
            StaleActivityMarkerDetected = true;
            _activityMarker.Delete();
        }

        _hardwareService.SnapshotUpdated += OnSnapshot;
        _watchdogTimer = new Timer(OnWatchdogTick, null, watchdogTimeout, watchdogPollInterval);
    }

    /// <summary>
    /// S8: true if a previous session's fan-control activity marker was still present at startup,
    /// meaning fans may still be at their last software-commanded speed from that session (only a
    /// PC restart, not relaunching PC Manager, hands them back to BIOS control). Read once by the
    /// Hardware page at startup.
    /// </summary>
    public bool StaleActivityMarkerDetected { get; }

    /// <summary>Raised whenever a safety rule needs a banner: overheat failsafe active, a set
    /// failure disabled software control, or fan control was paused/resumed. Always marshalled by
    /// the caller (the App's Hardware view model) onto the UI thread - this event itself may fire
    /// from the hardware thread, the watchdog timer thread, or the calling thread.</summary>
    public event EventHandler<FanControlAlert>? StatusChanged;

    /// <summary>
    /// Arms software fan control. Per spec 04, the "enabled" setting alone never starts control -
    /// the Hardware page calls this once it has successfully loaded the current sensors and fan
    /// profiles, so control never resumes silently at app startup before the page has shown it is
    /// active.
    /// </summary>
    public void Activate() => _hardwareService.RunOnOwnerThread(ArmCore, OwnerThreadTimeout);

    /// <summary>Clears any rule-4 disabled state and (re-)arms control. Called when the user turns
    /// the master switch on - whether for the first time this session or after a set failure had
    /// turned it back off - so a fresh enable never carries over a previous failure's disabled
    /// state.</summary>
    public void Rearm() => _hardwareService.RunOnOwnerThread(
        () =>
        {
            _engine.Reset();
            _pendingReadBacks.Clear();
            _failureAlertRaised = false;
            _watchdogTripped = false;
            ArmCore();
        },
        OwnerThreadTimeout);

    /// <summary>Rule 5: restores every known fan to default/BIOS control. Called by the app on
    /// exit, crash, system suspend, and session end - independent of the snapshot loop, so it works
    /// even if the hardware thread is not ticking right now. Always runs on the hardware thread
    /// (falling back to a direct call if that thread does not respond in time) so it never races a
    /// concurrent tick re-applying software control right after this restores it.</summary>
    public void RestoreAll() => _hardwareService.RunOnOwnerThread(RestoreAllCore, OwnerThreadTimeout);

    /// <summary>
    /// B2: pauses fan control (as if the master switch were off) and immediately restores every
    /// fan, *without* clearing the persisted "enabled" setting - unlike a set failure, this is not
    /// the user's fault and should not require re-confirming the risk warning. Stays paused across
    /// further ticks (a suspend/resume can complete within one tick, especially Modern Standby)
    /// until <see cref="ResumeFromSuspend"/> or <see cref="Rearm"/> explicitly clears it. Safe to
    /// call from any thread.
    /// </summary>
    public void Suspend(string reason)
    {
        lock (_stateLock)
        {
            _paused = true;
        }

        RestoreAll();
        RaiseStatus(FanControlAlertLevel.Caution, $"Fan control paused ({reason}).");
    }

    /// <summary>
    /// Clears a pause from <see cref="Suspend"/>. Per B2, this must be the *only* automatic way a
    /// pause clears - call this specifically for <c>PowerModes.Resume</c>, never on a timer or on
    /// the next tick regardless of cause. Forces a real hardware write on the next tick even if the
    /// engine's target happens to match what was last commanded before the pause, since LHM's
    /// <c>SetSoftware</c> is a no-op when the value has not changed from its own point of view.
    /// </summary>
    public void ResumeFromSuspend()
    {
        bool wasPaused;
        lock (_stateLock)
        {
            wasPaused = _paused;
            _paused = false;
        }

        if (wasPaused)
        {
            _hardwareService.RunOnOwnerThread(() => _forceRewriteAfterResume = true, OwnerThreadTimeout);
        }
    }

    public void Dispose()
    {
        _hardwareService.SnapshotUpdated -= OnSnapshot;
        _watchdogTimer.Dispose();
    }

    private void ArmCore()
    {
        lock (_stateLock)
        {
            _armed = true;
        }
    }

    private void OnSnapshot(object? sender, HardwareSnapshot snapshot)
    {
        try
        {
            OnSnapshotCore(snapshot);
            Interlocked.Exchange(ref _lastSnapshotProcessedUtcTicks, DateTime.UtcNow.Ticks);
        }
        catch (Exception ex)
        {
            // B3: an unhandled exception here (e.g. a duplicate sensor id, an unexpected null) must
            // never just vanish into the hardware-thread's own catch-all, which would silently skip
            // this tick's fan safety enforcement entirely. Treat it exactly like a set failure.
            _logger.LogError(ex, "Unexpected failure evaluating fan control; restoring every fan and disabling software control.");
            HandleSeriousFailure("Something went wrong evaluating fan control. Every fan was restored to automatic control and software fan control was turned off.");
        }
    }

    private void OnSnapshotCore(HardwareSnapshot snapshot)
    {
        var controllers = _hardwareService.Controllers;
        if (controllers.Count == 0)
        {
            return;
        }

        bool armed;
        bool paused;
        lock (_stateLock)
        {
            armed = _armed;
            paused = _paused;
        }

        var hardwareSettings = _settingsStore.Current.Hardware;
        var cpuGpuTemperatures = new Dictionary<string, SensorSample>();
        CollectTemperatures(snapshot.Nodes, cpuGpuTemperatures);

        // B3: if the hardware read itself is unhealthy, or we cannot see any CPU/GPU temperature at
        // all, the engine has nothing trustworthy to reason about - force every software-controlled
        // fan to full speed rather than let it keep whatever percent (e.g. a Fixed 30%) it last had
        // with no idea whether that is safe right now.
        var hasAnyCpuGpuReading = cpuGpuTemperatures.Values.Any(s => s.ValueC is not null);
        if (snapshot.Status != HardwareStatus.Ready || !hasAnyCpuGpuReading)
        {
            ForceAllControlledFansTo100(controllers, hardwareSettings);
            if (!_noTempFailsafeActive)
            {
                _noTempFailsafeActive = true;
                RaiseStatus(FanControlAlertLevel.Critical,
                    "No reliable CPU/GPU temperature reading is available - every controlled fan is at 100% until it is.");
            }

            return;
        }

        _noTempFailsafeActive = false;

        if (VerifyReadBacksAndHandleFailure(controllers))
        {
            return;
        }

        var softwareControlEnabled = armed && !paused && hardwareSettings.FanControlEnabled;
        var input = BuildInput(snapshot, hardwareSettings, cpuGpuTemperatures, controllers, softwareControlEnabled);
        var decision = _engine.Evaluate(input);

        var forceRewrite = _forceRewriteAfterResume;
        _forceRewriteAfterResume = false;
        var appliedAnySetPercent = false;

        foreach (var controller in controllers)
        {
            var target = decision.Targets.GetValueOrDefault(controller.Id, FanTarget.RestoreDefault);
            if (!Apply(controller, target, forceRewrite))
            {
                HandleSetFailure(controller);
                return;
            }

            TrackReadBack(controller, target);
            appliedAnySetPercent |= target.Kind == FanTargetKind.SetPercent;
        }

        UpdateActivityMarker(appliedAnySetPercent);

        if (decision.IsOverheatFailsafeActive)
        {
            RaiseStatus(FanControlAlertLevel.Critical,
                $"Overheat failsafe active ({decision.OverheatSensorId}): every fan is at 100% until it cools down.");
        }
    }

    /// <summary>S3: compares each controlled fan's actual duty cycle against what was last
    /// commanded; a fan stuck more than <see cref="ReadBackToleranceLevel"/> points off target for
    /// <see cref="ReadBackFailureStreak"/> consecutive ticks is a silent set failure some SuperIO/
    /// NVAPI backends can produce without ever throwing. Returns true if a failure was handled (and
    /// the caller should stop processing this tick).</summary>
    private bool VerifyReadBacksAndHandleFailure(IReadOnlyList<IFanController> controllers)
    {
        foreach (var controller in controllers)
        {
            if (!_pendingReadBacks.TryGetValue(controller.Id, out var pending))
            {
                continue;
            }

            var actual = controller.CurrentPercent;
            var withinTolerance = actual is { } value && Math.Abs(value - pending.ExpectedPercent) <= ReadBackToleranceLevel;

            if (withinTolerance)
            {
                _pendingReadBacks[controller.Id] = pending with { MismatchStreak = 0 };
                continue;
            }

            var streak = pending.MismatchStreak + 1;
            if (streak >= ReadBackFailureStreak)
            {
                _logger.LogError(
                    "Fan {FanId} stayed at {Actual} instead of the commanded {Expected} for {Streak} ticks; treating as a silent set failure.",
                    controller.Id, actual, pending.ExpectedPercent, streak);
                HandleSetFailure(controller);
                return true;
            }

            _pendingReadBacks[controller.Id] = pending with { MismatchStreak = streak };
        }

        return false;
    }

    private void TrackReadBack(IFanController controller, FanTarget target)
    {
        if (target.Kind == FanTargetKind.SetPercent)
        {
            _pendingReadBacks[controller.Id] = new PendingCheck(target.Percent, 0);
        }
        else
        {
            _pendingReadBacks.Remove(controller.Id);
        }
    }

    private void ForceAllControlledFansTo100(IReadOnlyList<IFanController> controllers, HardwareSettings settings)
    {
        var appliedAnySetPercent = false;
        foreach (var controller in controllers)
        {
            var profile = BuildProfile(controller.Id, settings);
            var target = profile.Mode == FanMode.Default ? FanTarget.RestoreDefault : FanTarget.SetPercent(100);
            Apply(controller, target, forceRewrite: false);
            appliedAnySetPercent |= target.Kind == FanTargetKind.SetPercent;
        }

        UpdateActivityMarker(appliedAnySetPercent);
    }

    private bool Apply(IFanController controller, FanTarget target, bool forceRewrite)
    {
        try
        {
            if (target.Kind == FanTargetKind.RestoreDefault)
            {
                controller.RestoreDefault();
            }
            else
            {
                if (forceRewrite)
                {
                    // B2: after a resume, LHM may consider the target "already set" from its last
                    // pre-suspend value and skip the hardware write - force a real one.
                    controller.RestoreDefault();
                }

                controller.SetPercent(target.Percent);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set fan {FanId} ({FanName}).", controller.Id, controller.Name);
            return false;
        }
    }

    /// <summary>Rule 4: the first time this fires, restores every fan, disables the persisted
    /// setting, and raises one critical banner. If the engine is already disabled (this is a repeat
    /// failure - e.g. <see cref="RestoreDefault"/> itself keeps throwing), it still tries to restore
    /// but does not re-alert or write settings again every tick.</summary>
    private void HandleSetFailure(IFanController failedController) =>
        HandleSeriousFailure($"Could not set {failedController.Name}'s speed. Every fan was restored to automatic control and software fan control was turned off.");

    private void HandleSeriousFailure(string message)
    {
        var alreadyDisabled = _engine.IsDisabledDueToError;
        _engine.MarkSetFailure();
        RestoreAllCore();
        _pendingReadBacks.Clear();

        if (alreadyDisabled && _failureAlertRaised)
        {
            // Already handled once; a RestoreDefault() that keeps throwing every tick must not spam
            // the log/settings/banner forever.
            return;
        }

        _failureAlertRaised = true;
        _settingsStore.Update(s => s.Hardware.FanControlEnabled = false);
        lock (_stateLock)
        {
            _armed = false;
        }

        RaiseStatus(FanControlAlertLevel.Critical, message);
    }

    private void RestoreAllCore()
    {
        foreach (var controller in _hardwareService.Controllers)
        {
            try
            {
                controller.RestoreDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore fan {FanId} ({FanName}) to default control.", controller.Id, controller.Name);
            }
        }

        UpdateActivityMarker(anySoftwareControlled: false);
    }

    private void UpdateActivityMarker(bool anySoftwareControlled)
    {
        if (anySoftwareControlled && !_activityMarkerActive)
        {
            _activityMarker.Create();
            _activityMarkerActive = true;
        }
        else if (!anySoftwareControlled && _activityMarkerActive)
        {
            _activityMarker.Delete();
            _activityMarkerActive = false;
        }
    }

    private void OnWatchdogTick(object? state)
    {
        var lastProcessed = new DateTime(Interlocked.Read(ref _lastSnapshotProcessedUtcTicks), DateTimeKind.Utc);
        var stalledFor = DateTime.UtcNow - lastProcessed;

        if (stalledFor <= _watchdogTimeout)
        {
            if (_watchdogTripped)
            {
                _watchdogTripped = false;
            }

            return;
        }

        if (_watchdogTripped)
        {
            return;
        }

        _watchdogTripped = true;
        _logger.LogError("No hardware snapshot processed for {Elapsed}; the hardware thread may be stuck. Restoring every fan.", stalledFor);
        HandleSeriousFailure("The hardware monitor stopped responding. Every fan was restored to automatic control and software fan control was turned off.");
    }

    private void RaiseStatus(FanControlAlertLevel level, string message) =>
        StatusChanged?.Invoke(this, new FanControlAlert(level, message));

    private static FanControlEngineInput BuildInput(
        HardwareSnapshot snapshot,
        HardwareSettings settings,
        Dictionary<string, SensorSample> cpuGpuTemperatures,
        IReadOnlyList<IFanController> controllers,
        bool softwareControlEnabled)
    {
        // A curve's source can be any temperature sensor (e.g. a motherboard/VRM sensor), not only
        // a CPU/GPU one, so look those up across every sensor rather than just cpuGpuTemperatures.
        // Built with an explicit loop (not ToDictionary) so a duplicate sensor id from LHM - however
        // unlikely - overwrites rather than throws and takes the whole tick down with it (B3).
        var allTemperatures = new Dictionary<string, SensorSample>();
        foreach (var sensor in snapshot.AllSensors())
        {
            if (sensor.Type == SensorType.Temperature && !SensorNaming.IsInvertedTemperature(sensor.Name))
            {
                allTemperatures[sensor.Id] = new SensorSample(sensor.Value, sensor.TimestampUtc);
            }
        }

        var profiles = new List<FanProfile>(controllers.Count);
        var fanSourceTemperatures = new Dictionary<string, SensorSample>();

        foreach (var controller in controllers)
        {
            var profile = BuildProfile(controller.Id, settings);
            profiles.Add(profile);

            if (profile is { Mode: FanMode.Curve, SourceSensorId: { } sourceId } &&
                allTemperatures.TryGetValue(sourceId, out var sample))
            {
                fanSourceTemperatures[sourceId] = sample;
            }
        }

        var failsafeTemperatureC = Math.Clamp(
            settings.FailsafeTemperatureC,
            FanControlOptions.LowestAllowedFailsafeTemperatureC,
            FanControlOptions.HighestAllowedFailsafeTemperatureC);

        return new FanControlEngineInput(
            SoftwareControlEnabled: softwareControlEnabled,
            MinPercent: Math.Max(settings.MinFanPercent, FanControlOptions.LowestAllowedMinPercent),
            FailsafeTemperatureC: failsafeTemperatureC,
            Profiles: profiles,
            CpuGpuTemperatures: cpuGpuTemperatures,
            FanSourceTemperatures: fanSourceTemperatures);
    }

    private static FanProfile BuildProfile(string fanId, HardwareSettings settings)
    {
        if (!settings.FanProfiles.TryGetValue(fanId, out var saved) || saved.Mode == FanMode.Default)
        {
            return new FanProfile(fanId, FanMode.Default);
        }

        if (saved.Mode == FanMode.Fixed)
        {
            return new FanProfile(fanId, FanMode.Fixed, FixedPercent: saved.FixedPercent);
        }

        var minPercent = Math.Max(settings.MinFanPercent, FanControlOptions.LowestAllowedMinPercent);
        if (saved.SourceSensorId is null ||
            !FanCurve.TryCreate(saved.CurvePoints, minPercent, out var curve, out _))
        {
            // An invalid/incomplete curve is treated the same as a lost sensor - rule 3 - rather
            // than silently falling back to BIOS control (which would look like control is off).
            // Note: a source sensor excluded for being an inverted-scale temperature (S1, e.g.
            // Intel's "Distance to TjMax") is handled the same way, one level up - such a sensor is
            // never present in the temperature lookup BuildInput builds, so the engine's normal
            // "source sensor missing" path (rule 3) already forces this fan to 100%.
            return new FanProfile(fanId, FanMode.Curve, SourceSensorId: saved.SourceSensorId, Curve: null);
        }

        return new FanProfile(fanId, FanMode.Curve, SourceSensorId: saved.SourceSensorId, Curve: curve);
    }

    private static void CollectTemperatures(IReadOnlyList<HardwareNode> nodes, Dictionary<string, SensorSample> into)
    {
        foreach (var node in nodes)
        {
            if (node.Type is HardwareNodeType.Cpu or HardwareNodeType.Gpu)
            {
                foreach (var sensor in node.Sensors)
                {
                    if (sensor.Type == SensorType.Temperature && !SensorNaming.IsInvertedTemperature(sensor.Name))
                    {
                        into[sensor.Id] = new SensorSample(sensor.Value, sensor.TimestampUtc);
                    }
                }
            }

            CollectTemperatures(node.Children, into);
        }
    }

    private readonly record struct PendingCheck(double ExpectedPercent, int MismatchStreak);
}
