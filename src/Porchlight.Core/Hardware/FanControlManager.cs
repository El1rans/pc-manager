using Microsoft.Extensions.Logging;
using Porchlight.Core.Settings;

namespace Porchlight.Core.Hardware;

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
/// <remarks>
/// N1: nothing in this class ever touches a fan unless control is actually meant to be active right
/// now - <see cref="IsControlActive"/> (armed, not paused, the persisted setting is on, and the
/// engine is not disabled from a previous failure) gates every path that would otherwise call
/// <see cref="IFanController.SetPercent"/> or <see cref="IFanController.RestoreDefault"/>, including
/// the unhealthy-snapshot failsafe. When control was never active, an unhealthy snapshot (not
/// elevated, driver missing, an error, or briefly no temperature reading) does precisely nothing -
/// there is nothing to protect and nothing this app is entitled to touch.
/// N1b: <see cref="RestoreDefault"/> is only ever called on a fan this instance has itself put into
/// software mode this session (<see cref="_ownedControllerIds"/>) - never on a fan some other
/// application already had under its own software control, and never as a reflexive "just in case"
/// on every launch.
/// </remarks>
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

    /// <summary>S3-2/known gap from PR #11: how many snapshot ticks a fan is allowed to keep
    /// reporting <see cref="IFanController.IsUnderSoftwareControl"/> after a <see cref="IFanController.RestoreDefault"/>
    /// call before this treats it as stuck and retries. Read-only verification - see
    /// <see cref="VerifyPendingRestoresAndRetry"/> - this never calls <see cref="IFanController.SetPercent"/>.</summary>
    private const int RestoreVerificationTickThreshold = 3;

    /// <summary>Total <see cref="IFanController.RestoreDefault"/> calls allowed for one hand-back
    /// (the original call plus up to 3 retries) before giving up and raising a critical banner.</summary>
    private const int MaxRestoreAttempts = 4;

    /// <summary>Independent watchdog default: if no evaluation has happened while control was
    /// actually active for this long, the hardware thread itself may be stuck or dead. Overridable
    /// by the internal test constructor only.</summary>
    private static readonly TimeSpan DefaultWatchdogTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan DefaultWatchdogPollInterval = TimeSpan.FromSeconds(1);

    private readonly IHardwareService _hardwareService;
    private readonly ISettingsStore _settingsStore;
    private readonly FanControlEngine _engine;
    private readonly IFanControlActivityMarker _activityMarker;
    private readonly ILogger<FanControlManager> _logger;
    private readonly Lock _stateLock = new();
    private readonly Dictionary<string, PendingCheck> _pendingReadBacks = [];

    /// <summary>S3-2: fans currently being verified after a <see cref="IFanController.RestoreDefault"/>
    /// hand-back - see <see cref="VerifyPendingRestoresAndRetry"/>. Keyed by controller id.</summary>
    private readonly Dictionary<string, RestorePendingState> _pendingRestoreVerifications = [];

    /// <summary>N1b: ids of fans this instance has itself called <see cref="IFanController.SetPercent"/>
    /// on this session. <see cref="IFanController.RestoreDefault"/> is only ever called for an id in
    /// this set - see the type-level remarks.</summary>
    private readonly HashSet<string> _ownedControllerIds = [];

    private readonly TimeProvider _timeProvider;
    private readonly ITimer _watchdogTimer;
    private readonly TimeSpan _watchdogTimeout;

    private bool _armed;
    private bool _paused;

    /// <summary>N5: only a pause raised for <c>PowerModes.Suspend</c> is eligible to be cleared by
    /// <see cref="ResumeFromSuspend"/>; a pause from a crash handler or session-ending stays until
    /// the user explicitly re-arms.</summary>
    private bool _pausedByResumableSuspend;

    private bool _failureAlertRaised;
    private bool _noTempFailsafeActive;
    private bool _forceRewriteAfterResume;
    private bool _activityMarkerActive;
    private long _lastActiveEvaluationUtcTicks;
    private bool _watchdogTripped;

    public FanControlManager(
        IHardwareService hardwareService,
        ISettingsStore settingsStore,
        FanControlEngine engine,
        IFanControlActivityMarker activityMarker,
        ILogger<FanControlManager> logger)
        : this(hardwareService, settingsStore, engine, activityMarker, logger, DefaultWatchdogTimeout, DefaultWatchdogPollInterval, TimeProvider.System)
    {
    }

    /// <summary>Test seam: lets tests use a short watchdog timeout/poll interval - and a fake
    /// <see cref="TimeProvider"/> (e.g. <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>) -
    /// instead of a real clock/timer, so the watchdog's "now" reads and its timer never depend on
    /// wall-clock timing. Production behaviour is unchanged: the public constructor always passes
    /// <see cref="TimeProvider.System"/>.</summary>
    internal FanControlManager(
        IHardwareService hardwareService,
        ISettingsStore settingsStore,
        FanControlEngine engine,
        IFanControlActivityMarker activityMarker,
        ILogger<FanControlManager> logger,
        TimeSpan watchdogTimeout,
        TimeSpan watchdogPollInterval,
        TimeProvider timeProvider)
    {
        _hardwareService = hardwareService;
        _settingsStore = settingsStore;
        _engine = engine;
        _activityMarker = activityMarker;
        _logger = logger;
        _watchdogTimeout = watchdogTimeout;
        _timeProvider = timeProvider;
        _lastActiveEvaluationUtcTicks = _timeProvider.GetUtcNow().UtcDateTime.Ticks;

        // S8: a marker still present at construction means the previous session ended without
        // going through any restore path (forced kill, crash, BSOD, power loss) - surface it once,
        // then clear it so the warning does not repeat on every future launch.
        if (_activityMarker.Exists())
        {
            StaleActivityMarkerDetected = true;
            _activityMarker.Delete();
        }

        _hardwareService.SnapshotUpdated += OnSnapshot;
        _watchdogTimer = _timeProvider.CreateTimer(OnWatchdogTick, null, watchdogTimeout, watchdogPollInterval);
    }

    /// <summary>
    /// S8: true if a previous session's fan-control activity marker was still present at startup,
    /// meaning fans may still be at their last software-commanded speed from that session (only a
    /// PC restart, not relaunching Porchlight, hands them back to BIOS control). Read once by the
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
    public void Activate() => _hardwareService.RunOnOwnerThread(ArmCore, OwnerThreadTimeout, allowDirectFallback: false);

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
            lock (_stateLock)
            {
                _paused = false;
                _pausedByResumableSuspend = false;
            }

            ArmCore();
        },
        OwnerThreadTimeout,
        allowDirectFallback: false);

    /// <summary>Rule 5: restores every fan this instance owns (N1b) to default/BIOS control. Called
    /// by the app on exit, crash, system suspend, and session end - independent of the snapshot
    /// loop, so it works even if the hardware thread is not ticking right now. Always runs on the
    /// hardware thread (falling back to a direct call if that thread does not respond in time) so it
    /// never races a concurrent tick re-applying software control right after this restores it.</summary>
    public void RestoreAll() => _hardwareService.RunOnOwnerThread(RestoreAllCore, OwnerThreadTimeout);

    /// <summary>
    /// B2: pauses fan control (as if the master switch were off) and immediately restores every fan
    /// this instance owns, *without* clearing the persisted "enabled" setting - unlike a set
    /// failure, this is not the user's fault and should not require re-confirming the risk warning.
    /// Stays paused across further ticks (a suspend/resume can complete within one tick, especially
    /// Modern Standby) until <see cref="ResumeFromSuspend"/> (only when <paramref name="reason"/> was
    /// itself a resumable system suspend) or <see cref="Rearm"/> explicitly clears it. Safe to call
    /// from any thread.
    /// </summary>
    /// <param name="reason">Shown in the banner.</param>
    /// <param name="resumableBySystemResume">N5: true only for an actual <c>PowerModes.Suspend</c> -
    /// a crash handler or session-ending pause must stay paused until the user re-arms, never
    /// clearing automatically.</param>
    public void Suspend(string reason, bool resumableBySystemResume)
    {
        lock (_stateLock)
        {
            _paused = true;
            _pausedByResumableSuspend = resumableBySystemResume;
        }

        RestoreAll();
        RaiseStatus(FanControlAlertLevel.Caution, $"Fan control paused ({reason}).");
    }

    /// <summary>
    /// Clears a pause raised by <see cref="Suspend"/> with <c>resumableBySystemResume: true</c> -
    /// per N5, this must be the *only* automatic way such a pause clears, and it must never clear a
    /// pause from a crash handler or session-ending. Call this specifically for
    /// <c>PowerModes.Resume</c>, never on a timer or on the next tick regardless of cause. Forces a
    /// real hardware write on the next tick even if the engine's target happens to match what was
    /// last commanded before the pause, since LHM's <c>SetSoftware</c> is a no-op when the value has
    /// not changed from its own point of view.
    /// </summary>
    public void ResumeFromSuspend()
    {
        bool shouldResume;
        lock (_stateLock)
        {
            shouldResume = _paused && _pausedByResumableSuspend;
            if (shouldResume)
            {
                // R3-2: reset the watchdog's clock *before* clearing the pause, both under the same
                // lock the watchdog itself reads _paused under (see OnWatchdogTick). Otherwise a
                // watchdog tick landing in between the two could observe "not paused any more" with
                // the clock still holding its pre-sleep value - and a sleep longer than the
                // watchdog's timeout (easily true for a real system suspend) would then look like an
                // immediate, multi-hour stall and re-trip before this method has even returned.
                Interlocked.Exchange(ref _lastActiveEvaluationUtcTicks, _timeProvider.GetUtcNow().UtcDateTime.Ticks);
                _paused = false;
                _pausedByResumableSuspend = false;
            }
        }

        if (shouldResume)
        {
            _hardwareService.RunOnOwnerThread(() => _forceRewriteAfterResume = true, OwnerThreadTimeout, allowDirectFallback: false);
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

        Interlocked.Exchange(ref _lastActiveEvaluationUtcTicks, _timeProvider.GetUtcNow().UtcDateTime.Ticks);
    }

    /// <summary>N1/N3: whether fan control is currently meant to be doing anything at all. Every
    /// path that could call <see cref="IFanController.SetPercent"/> - including the unhealthy-
    /// snapshot failsafe - is gated on this; when it is false, an unhealthy snapshot is expected
    /// (e.g. simply not elevated yet) and must never itself start touching fans.</summary>
    private bool IsControlActive()
    {
        lock (_stateLock)
        {
            return _armed && !_paused && _settingsStore.Current.Hardware.FanControlEnabled && !_engine.IsDisabledDueToError;
        }
    }

    private void OnSnapshot(object? sender, HardwareSnapshot snapshot)
    {
        try
        {
            OnSnapshotCore(snapshot);
        }
        catch (Exception ex)
        {
            // An unhandled exception here (e.g. a duplicate sensor id, an unexpected null) must
            // never just vanish into the hardware thread's own catch-all, which would silently skip
            // this tick's fan safety enforcement entirely.
            _logger.LogError(ex, "Unexpected failure evaluating fan control.");
            if (IsControlActive())
            {
                HandleSeriousFailure("Something went wrong evaluating fan control. Every fan under software control was restored to automatic control and software fan control was turned off.");
            }
        }
    }

    private void OnSnapshotCore(HardwareSnapshot snapshot)
    {
        var controllers = _hardwareService.Controllers;
        if (controllers.Count == 0)
        {
            return;
        }

        // S3-2: independent of whether control is active right now - a restore issued just before
        // control went inactive (rule 4, a dropped snapshot, RestoreAll on exit/suspend) still needs
        // to be verified on the ticks that follow.
        VerifyPendingRestoresAndRetry(controllers);

        var controlActive = IsControlActive();

        if (!controlActive)
        {
            // N1: nothing to evaluate, and nothing to touch. In particular, an unhealthy snapshot
            // (not elevated, driver missing, an error) while control was never armed/enabled must do
            // precisely nothing to any fan - there is no software-commanded state to protect.
            return;
        }

        // Only reached while control is genuinely active - safe to advance the watchdog's clock.
        // Deliberately advanced here, before the Ready check below, rather than only while Ready:
        // an extended-but-legitimate non-Ready period (e.g. running non-elevated for a while with a
        // profile saved from an earlier elevated session) must not itself look like a stuck hardware
        // thread to the watchdog - see R3-1/R3-3.
        Interlocked.Exchange(ref _lastActiveEvaluationUtcTicks, _timeProvider.GetUtcNow().UtcDateTime.Ticks);

        if (snapshot.Status == HardwareStatus.Error)
        {
            // The hardware read itself failed - nothing here is trustworthy, including whether the
            // fans we think we own are even the same physical fans any more. Restore what we own
            // (rule 4's response) rather than guessing at 100%.
            HandleSeriousFailure("Could not read hardware sensors. Every fan under software control was restored to automatic control and software fan control was turned off.");
            return;
        }

        if (snapshot.Status != HardwareStatus.Ready)
        {
            // R3-1: hardware access has dropped (not elevated, or the driver is missing) - most
            // commonly a fan profile saved during an earlier *elevated* session, now running
            // non-elevated. This is not the user's fault and must not disable the persisted setting
            // (unlike rule 4) - just hand back anything this instance is actively driving. Without
            // this check, a vendor-API-only fan (e.g. an NVIDIA GPU fan, controllable without
            // PawnIO) could still be commanded by SetPercent while every UI element says fan control
            // is unavailable, which is exactly what round 3's live probe proved.
            RestoreAllCore();
            return;
        }

        var hardwareSettings = _settingsStore.Current.Hardware;
        var cpuGpuTemperatures = new Dictionary<string, SensorSample>();
        CollectTemperatures(snapshot.Nodes, cpuGpuTemperatures);

        // R3-3: the no-reliable-temperature failsafe below is specifically about *CPU* temperature -
        // matching its own banner text ("No reliable CPU temperature reading") - not "CPU or GPU".
        // A GPU-only reading (e.g. from a vendor API with no CPU sensor exposed at all) must not
        // count as "we have something trustworthy to reason about" for this check; rule 2's
        // overheat failsafe below still legitimately watches both CPU and GPU temperatures.
        var hasLiveCpuTemperature = HasLiveCpuTemperature(snapshot.Nodes);

        if (!hasLiveCpuTemperature)
        {
            // A live (Ready) snapshot with no CPU temperature reading at all (rule 2/3's territory:
            // the engine has nothing trustworthy to reason about) - force every owned, controlled
            // fan to full speed rather than leave it at whatever percent (e.g. a Fixed 30%) it last
            // had.
            ForceAllOwnedControlledFansTo100(controllers, hardwareSettings);
            if (!_noTempFailsafeActive)
            {
                _noTempFailsafeActive = true;
                RaiseStatus(FanControlAlertLevel.Critical,
                    "No reliable CPU temperature reading is available - every controlled fan is at 100% until it is.");
            }

            return;
        }

        _noTempFailsafeActive = false;

        if (VerifyReadBacksAndHandleFailure(controllers))
        {
            return;
        }

        var input = BuildInput(snapshot, hardwareSettings, cpuGpuTemperatures, controllers, softwareControlEnabled: true);
        var decision = _engine.Evaluate(input);

        var forceRewrite = _forceRewriteAfterResume;
        _forceRewriteAfterResume = false;
        var appliedAnySetPercent = false;

        foreach (var controller in controllers)
        {
            var target = decision.Targets.GetValueOrDefault(controller.Id, FanTarget.RestoreDefault);
            if (!Apply(controller, target, forceRewrite, out var clampedPercent))
            {
                HandleSetFailure(controller);
                return;
            }

            TrackReadBack(controller, target, clampedPercent);
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
    /// commanded (the *clamped* value actually sent - N6); a fan stuck more than
    /// <see cref="ReadBackToleranceLevel"/> points off target for <see cref="ReadBackFailureStreak"/>
    /// consecutive ticks is a silent set failure some SuperIO/NVAPI backends can produce without
    /// ever throwing. Returns true if a failure was handled (and the caller should stop processing
    /// this tick).</summary>
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

    private void TrackReadBack(IFanController controller, FanTarget target, double clampedPercent)
    {
        if (target.Kind == FanTargetKind.SetPercent)
        {
            _pendingReadBacks[controller.Id] = new PendingCheck(clampedPercent, 0);

            // S3-2: a fresh SetPercent means this fan is being actively (re-)commanded, not handed
            // back - cancel any restore verification in flight for it, so a slow read-back retry
            // from an earlier hand-back can never fight a deliberate, later SetPercent.
            _pendingRestoreVerifications.Remove(controller.Id);
        }
        else
        {
            _pendingReadBacks.Remove(controller.Id);
        }
    }

    /// <summary>S3-2: starts (or keeps, if already in progress) verifying that <paramref name="controllerId"/>
    /// actually left software control after a <see cref="IFanController.RestoreDefault"/> hand-back -
    /// see <see cref="VerifyPendingRestoresAndRetry"/>. Only registers on the *first* call for a given
    /// hand-back; callers that call <see cref="IFanController.RestoreDefault"/> again every tick while
    /// a profile stays at <see cref="FanMode.Default"/> must not keep resetting the verification
    /// window each time.</summary>
    private void RegisterPendingRestoreVerification(string controllerId)
    {
        if (!_pendingRestoreVerifications.ContainsKey(controllerId))
        {
            _pendingRestoreVerifications[controllerId] = new RestorePendingState(Attempts: 1, TicksSinceLastAttempt: 0);
        }
    }

    /// <summary>
    /// S3-2/known gap from PR #11: after a <see cref="IFanController.RestoreDefault"/> hand-back
    /// (<see cref="RegisterPendingRestoreVerification"/>), checks on every following tick whether the
    /// control sensor still reports <see cref="IFanController.IsUnderSoftwareControl"/>. A fan that
    /// keeps reporting software control for <see cref="RestoreVerificationTickThreshold"/> consecutive
    /// ticks gets one more <see cref="IFanController.RestoreDefault"/> retry; after
    /// <see cref="MaxRestoreAttempts"/> total attempts still fail to clear it, this gives up and raises
    /// a critical banner instead of retrying forever. Never calls <see cref="IFanController.SetPercent"/>.
    /// </summary>
    private void VerifyPendingRestoresAndRetry(IReadOnlyList<IFanController> controllers)
    {
        if (_pendingRestoreVerifications.Count == 0)
        {
            return;
        }

        foreach (var controllerId in _pendingRestoreVerifications.Keys.ToList())
        {
            var controller = controllers.FirstOrDefault(c => c.Id == controllerId);
            if (controller is null)
            {
                // The hardware set changed (re-enumeration) - nothing left to verify.
                _pendingRestoreVerifications.Remove(controllerId);
                continue;
            }

            if (!controller.IsUnderSoftwareControl)
            {
                // Confirmed back under BIOS/EC control.
                _pendingRestoreVerifications.Remove(controllerId);
                continue;
            }

            var state = _pendingRestoreVerifications[controllerId];
            var ticks = state.TicksSinceLastAttempt + 1;
            if (ticks < RestoreVerificationTickThreshold)
            {
                _pendingRestoreVerifications[controllerId] = state with { TicksSinceLastAttempt = ticks };
                continue;
            }

            if (state.Attempts >= MaxRestoreAttempts)
            {
                _pendingRestoreVerifications.Remove(controllerId);
                _logger.LogError(
                    "Fan {FanId} ({FanName}) still reports software control after {Attempts} RestoreDefault attempts.",
                    controller.Id, controller.Name, state.Attempts);
                RaiseStatus(FanControlAlertLevel.Critical,
                    "A fan may still be under software control. Restart your PC to return it to BIOS control.");
                continue;
            }

            try
            {
                controller.RestoreDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retry {Attempt} of RestoreDefault failed for fan {FanId} ({FanName}).",
                    state.Attempts + 1, controller.Id, controller.Name);
            }

            _pendingRestoreVerifications[controllerId] = new RestorePendingState(state.Attempts + 1, TicksSinceLastAttempt: 0);
        }
    }

    private void ForceAllOwnedControlledFansTo100(IReadOnlyList<IFanController> controllers, HardwareSettings settings)
    {
        var appliedAnySetPercent = false;
        foreach (var controller in controllers)
        {
            var profile = BuildProfile(controller.Id, settings);
            var target = profile.Mode == FanMode.Default ? FanTarget.RestoreDefault : FanTarget.SetPercent(100);
            if (!Apply(controller, target, forceRewrite: false, out _))
            {
                // N6: a failure here must not be silently ignored either.
                HandleSetFailure(controller);
                return;
            }

            appliedAnySetPercent |= target.Kind == FanTargetKind.SetPercent;
        }

        UpdateActivityMarker(appliedAnySetPercent);
    }

    /// <summary>
    /// N1b: only ever calls <see cref="IFanController.RestoreDefault"/> on a fan already in
    /// <see cref="_ownedControllerIds"/> - one this instance itself previously set into software
    /// mode. N6: the percent actually sent to <see cref="IFanController.SetPercent"/> (and returned
    /// via <paramref name="clampedPercent"/> for read-back tracking) is clamped against the
    /// controller's own <see cref="IFanController.MinSoftwarePercent"/>/<see cref="IFanController.MaxSoftwarePercent"/>
    /// here, matching what <see cref="LhmFanController.SetPercent"/> will itself clamp to - so the
    /// two can never disagree and produce a false read-back mismatch.
    /// </summary>
    private bool Apply(IFanController controller, FanTarget target, bool forceRewrite, out double clampedPercent)
    {
        clampedPercent = 0;

        try
        {
            if (target.Kind == FanTargetKind.RestoreDefault)
            {
                if (_ownedControllerIds.Contains(controller.Id))
                {
                    controller.RestoreDefault();
                    RegisterPendingRestoreVerification(controller.Id);
                }

                return true;
            }

            var lowerBound = Math.Max(FanControlOptions.LowestAllowedMinPercent, controller.MinSoftwarePercent);
            var upperBound = Math.Max(lowerBound, controller.MaxSoftwarePercent);
            clampedPercent = double.IsNaN(target.Percent) ? upperBound : Math.Clamp(target.Percent, lowerBound, upperBound);

            if (forceRewrite && _ownedControllerIds.Contains(controller.Id))
            {
                // B2: after a resume, LHM may consider the target "already set" from its last
                // pre-suspend value and skip the hardware write - force a real one.
                controller.RestoreDefault();
            }

            controller.SetPercent(clampedPercent);
            _ownedControllerIds.Add(controller.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set fan {FanId} ({FanName}).", controller.Id, controller.Name);
            return false;
        }
    }

    /// <summary>Rule 4: the first time this fires, restores every owned fan, disables the persisted
    /// setting, and raises one critical banner. If the engine is already disabled (this is a repeat
    /// failure - e.g. <see cref="RestoreDefault"/> itself keeps throwing), it still tries to restore
    /// but does not re-alert or write settings again every tick.</summary>
    private void HandleSetFailure(IFanController failedController) =>
        HandleSeriousFailure($"Could not set {failedController.Name}'s speed. Every fan under software control was restored to automatic control and software fan control was turned off.");

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

    /// <summary>N1b: restores only fans in <see cref="_ownedControllerIds"/> - see the type-level
    /// remarks. A session where control was never enabled has an empty owned set, so this is a
    /// complete no-op, touching nothing.</summary>
    private void RestoreAllCore()
    {
        foreach (var controller in _hardwareService.Controllers)
        {
            if (!_ownedControllerIds.Contains(controller.Id))
            {
                continue;
            }

            try
            {
                controller.RestoreDefault();
                RegisterPendingRestoreVerification(controller.Id);
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

    /// <summary>
    /// N3: only ever acts while control is genuinely active right now (armed, enabled, not paused,
    /// not already disabled, and actually owning at least one fan) - otherwise it silently resets its
    /// own tripped state and does nothing, so it can never itself be the reason software fan control
    /// gets turned off in a session where it was never turned on. Its clock
    /// (<see cref="_lastActiveEvaluationUtcTicks"/>) is only ever advanced from
    /// <see cref="OnSnapshotCore"/> while active, and reset the moment control is (re-)armed, so time
    /// spent inactive (or during a brief re-initialization, which still produces a snapshot well
    /// under the timeout in practice) never counts as a stall.
    /// </summary>
    private void OnWatchdogTick(object? state)
    {
        bool active;
        lock (_stateLock)
        {
            active = _armed && !_paused && _settingsStore.Current.Hardware.FanControlEnabled &&
                !_engine.IsDisabledDueToError && _ownedControllerIds.Count > 0;
        }

        if (!active)
        {
            _watchdogTripped = false;
            return;
        }

        var lastActive = new DateTime(Interlocked.Read(ref _lastActiveEvaluationUtcTicks), DateTimeKind.Utc);
        var stalledFor = _timeProvider.GetUtcNow().UtcDateTime - lastActive;
        if (stalledFor <= _watchdogTimeout)
        {
            _watchdogTripped = false;
            return;
        }

        if (_watchdogTripped)
        {
            return;
        }

        _watchdogTripped = true;
        _logger.LogError("No active fan-control evaluation for {Elapsed}; the hardware thread may be stuck. Pausing and restoring every owned fan.", stalledFor);

        // N3: a stall pauses (like a suspend) rather than disabling the persisted setting - the
        // hardware thread recovering on its own (or the user restarting the app) should not require
        // re-confirming the risk warning, unlike an actual rule-4 set failure.
        Suspend("hardware monitor stopped responding", resumableBySystemResume: false);
        RaiseStatus(FanControlAlertLevel.Critical,
            "The hardware monitor stopped responding. Every fan under software control was restored to automatic control.");
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

    /// <summary>R3-3: specifically a *CPU* temperature - see the call site's comment. Does not
    /// consider an inverted-scale sensor (S1, e.g. Intel's "Distance to TjMax") live, for the same
    /// reason those are excluded everywhere else.</summary>
    private static bool HasLiveCpuTemperature(IReadOnlyList<HardwareNode> nodes) =>
        nodes.Any(n =>
            (n.Type == HardwareNodeType.Cpu &&
                n.Sensors.Any(s => s.Type == SensorType.Temperature && s.Value is not null && !SensorNaming.IsInvertedTemperature(s.Name))) ||
            HasLiveCpuTemperature(n.Children));

    private readonly record struct PendingCheck(double ExpectedPercent, int MismatchStreak);

    /// <summary>S3-2: <paramref name="Attempts"/> counts total <see cref="IFanController.RestoreDefault"/>
    /// calls made for this hand-back (the original plus retries); <paramref name="TicksSinceLastAttempt"/>
    /// counts snapshot ticks observed since the most recent one, reset on every retry.</summary>
    private readonly record struct RestorePendingState(int Attempts, int TicksSinceLastAttempt);
}
