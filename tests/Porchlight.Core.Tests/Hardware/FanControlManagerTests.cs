using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Hardware;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

public sealed class FanControlManagerTests : IDisposable
{
    private const string FanId = "fan1";

    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeHardwareService _hardwareService = new();
    private readonly FakeFanController _fan = new(FanId);
    private readonly FakeFanControlActivityMarker _marker = new();
    private readonly FanControlEngine _engine = new(new FakeClock());
    private readonly FakeTimeProvider _timeProvider = new();

    public FanControlManagerTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PorchlightTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _settingsStore = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_directory, "settings.json"));
        _hardwareService.Controllers = [_fan];
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private FanControlManager CreateManager(TimeSpan? watchdogTimeout = null, TimeSpan? watchdogPollInterval = null) =>
        new(_hardwareService,
            _settingsStore,
            _engine,
            _marker,
            NullLogger<FanControlManager>.Instance,
            watchdogTimeout ?? TimeSpan.FromMinutes(10),
            watchdogPollInterval ?? TimeSpan.FromMinutes(10),
            _timeProvider);

    private void EnableFixedFan(double fixedPercent = 40)
    {
        _settingsStore.Update(s =>
        {
            s.Hardware.FanControlEnabled = true;
            s.Hardware.FanProfiles[FanId] = new FanProfileSettings { Mode = FanMode.Fixed, FixedPercent = fixedPercent };
        });
    }

    private static HardwareSnapshot HealthySnapshotWithCpuTemp(double cpuTempC) =>
        new(
            HardwareStatus.Ready,
            null,
            [new HardwareNode("cpu", "CPU", HardwareNodeType.Cpu,
                [new SensorReading("cpu/temp", "CPU Package", SensorType.Temperature, cpuTempC, null, null, DateTimeOffset.UtcNow)],
                [])],
            DateTimeOffset.UtcNow);

    // ---------------------------------------------------------------- N1: never touch a fan unless control is active

    [Fact]
    public void OnSnapshot_MasterOff_NeverElevatedSnapshot_NoSetPercentCalls()
    {
        // FanControlEnabled defaults to false; never call Activate() either.
        _settingsStore.Update(s => s.Hardware.FanProfiles[FanId] = new FanProfileSettings { Mode = FanMode.Fixed, FixedPercent = 40 });
        using var manager = CreateManager();

        _hardwareService.RaiseSnapshot(HardwareSnapshot.Empty(HardwareStatus.NotElevated));

        Assert.Equal(0, _fan.SetPercentCallCount);
        Assert.Equal(0, _fan.RestoreDefaultCallCount);
        Assert.Null(_fan.CurrentPercent);
    }

    [Fact]
    public void OnSnapshot_NeverActivated_EvenWithEnabledSettingAndHealthySnapshot_NoSetPercentCalls()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        // Deliberately never call manager.Activate().

        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(0, _fan.SetPercentCallCount);
    }

    [Fact]
    public void OnSnapshot_Paused_NoSetPercentCalls()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        manager.Suspend("test", resumableBySystemResume: true);

        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(0, _fan.SetPercentCallCount);
    }

    [Fact]
    public void OnSnapshot_EngineDisabledDueToPriorError_NoFurtherSetPercentCalls()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(40, _fan.CurrentPercent);

        _fan.ThrowOnSetPercent = true;
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(60)); // triggers rule 4, disables

        var callsAfterFailure = _fan.SetPercentCallCount;
        _fan.ThrowOnSetPercent = false;
        _fan.CurrentPercent = 999; // prove nothing touches it again

        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(callsAfterFailure, _fan.SetPercentCallCount);
        Assert.Equal(999, _fan.CurrentPercent);
    }

    [Fact]
    public void OnSnapshot_NotElevatedWithSavedFixedProfile_ArmedAndEnabled_NeverCallsSetPercent()
    {
        // R3-1: even with control armed+enabled (e.g. a profile saved during an earlier *elevated*
        // session), a NotElevated/DriverMissing snapshot must never call SetPercent at all - a
        // vendor-API-only fan (NVIDIA/AMD GPU fans, controllable without PawnIO) would otherwise
        // still get commanded while every UI element says fan control is unavailable. Control is
        // only ever active while the snapshot's own Status is Ready.
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();

        _hardwareService.RaiseSnapshot(HardwareSnapshot.Empty(HardwareStatus.NotElevated));

        Assert.Equal(0, _fan.SetPercentCallCount);
        Assert.Null(_fan.CurrentPercent);
    }

    [Fact]
    public void OnSnapshot_DriverMissingWithSavedFixedProfile_ArmedAndEnabled_NeverCallsSetPercent()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();

        _hardwareService.RaiseSnapshot(HardwareSnapshot.Empty(HardwareStatus.DriverMissing));

        Assert.Equal(0, _fan.SetPercentCallCount);
    }

    [Fact]
    public void OnSnapshot_ReadyThenDropsToNotElevated_RestoresOwnedFan_WithoutDisablingSetting()
    {
        // R3-1's Ready->NotElevated transition: a fan already owned from an earlier Ready tick must
        // be handed back, but this is not the user's fault (unlike rule 4) - the persisted setting
        // stays on so it resumes on its own once elevation/the driver come back.
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(40, _fan.CurrentPercent);

        _hardwareService.RaiseSnapshot(HardwareSnapshot.Empty(HardwareStatus.NotElevated));

        Assert.Null(_fan.CurrentPercent); // restored, not left at 40 and not forced to 100
        Assert.True(_settingsStore.Current.Hardware.FanControlEnabled); // not disabled - not rule 4
    }

    // ---------------------------------------------------------------- N1b: never restore a fan we don't own

    [Fact]
    public void NeverEnabledSession_RestoreAllAndSuspendAndCloseAllNoOp_ZeroRestoreDefaultCalls()
    {
        using var manager = CreateManager();
        // Fan control never activated, never enabled - nothing owns this fan.

        manager.RestoreAll();
        manager.Suspend("test", resumableBySystemResume: true);

        Assert.Equal(0, _fan.RestoreDefaultCallCount);
    }

    [Fact]
    public void OwnedFan_RestoreAllRestoresIt_UnownedFanUntouched()
    {
        var unowned = new FakeFanController("fan2");
        _hardwareService.Controllers = [_fan, unowned];

        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(1, _fan.SetPercentCallCount); // fan1 is now owned; fan2 has no profile (Default)

        manager.RestoreAll();

        Assert.True(_fan.RestoreDefaultCallCount > 0);
        Assert.Equal(0, unowned.RestoreDefaultCallCount);
    }

    // ---------------------------------------------------------------- healthy path still works

    [Fact]
    public void OnSnapshot_ActivatedEnabledHealthySnapshot_AppliesFixedTargetNormally()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();

        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(40, _fan.CurrentPercent);
    }

    [Fact]
    public void OnSnapshot_NoCpuTemperature_WhileActiveAndOwning_ForcesTo100()
    {
        EnableFixedFan(30);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(30, _fan.CurrentPercent);

        var snapshotWithNoTemps = new HardwareSnapshot(HardwareStatus.Ready, null, [], DateTimeOffset.UtcNow);
        _hardwareService.RaiseSnapshot(snapshotWithNoTemps);

        Assert.Equal(100, _fan.CurrentPercent);
    }

    [Fact]
    public void OnSnapshot_GpuTemperatureButNoCpuTemperature_StillForcesTo100()
    {
        // R3-3: the no-reliable-temperature failsafe is specifically about CPU temperature (matching
        // its own banner text) - a GPU-only reading (e.g. from a vendor API with no CPU sensor
        // exposed) must not count as "something trustworthy", even though rule 2's overheat failsafe
        // legitimately watches both CPU and GPU.
        EnableFixedFan(30);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(30, _fan.CurrentPercent);

        var gpuOnlySnapshot = new HardwareSnapshot(
            HardwareStatus.Ready,
            null,
            [new HardwareNode("gpu", "GPU", HardwareNodeType.Gpu,
                [new SensorReading("gpu/temp", "GPU Core", SensorType.Temperature, 60, null, null, DateTimeOffset.UtcNow)],
                [])],
            DateTimeOffset.UtcNow);
        _hardwareService.RaiseSnapshot(gpuOnlySnapshot);

        Assert.Equal(100, _fan.CurrentPercent);
    }

    [Fact]
    public void OnSnapshot_ErrorStatus_WhileActiveAndOwning_RestoresDefaultAndDisables()
    {
        EnableFixedFan(30);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(30, _fan.CurrentPercent);

        _hardwareService.RaiseSnapshot(HardwareSnapshot.Empty(HardwareStatus.Error, "boom"));

        Assert.Null(_fan.CurrentPercent); // restored to default, not forced to 100
        Assert.False(_settingsStore.Current.Hardware.FanControlEnabled);
    }

    // ---------------------------------------------------------------- rule 4: set failure

    [Fact]
    public void OnSnapshot_SetPercentThrows_RestoresOwnedAndDisablesSetting()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50)); // establishes ownership
        _fan.ThrowOnSetPercent = true;

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(60));

        Assert.False(_settingsStore.Current.Hardware.FanControlEnabled);
        Assert.True(_fan.RestoreDefaultCallCount > 0);
        Assert.NotNull(alert);
        Assert.Equal(FanControlAlertLevel.Critical, alert!.Level);
    }

    // ---------------------------------------------------------------- N6: read-back compares the clamped value

    [Fact]
    public void OnSnapshot_TargetBelowControllerMinimum_ReadBackComparesClampedValue_NoFalseTrip()
    {
        // The engine's own floor (MinFanPercent) is lower than this specific channel's hardware
        // minimum - SetPercent will clamp up to 35, and the read-back check must expect 35, not the
        // engine's raw 20.
        _fan.MinSoftwarePercent = 35;
        _settingsStore.Update(s =>
        {
            s.Hardware.FanControlEnabled = true;
            s.Hardware.MinFanPercent = 20;
            s.Hardware.FanProfiles[FanId] = new FanProfileSettings { Mode = FanMode.Fixed, FixedPercent = 20 };
        });
        using var manager = CreateManager();
        manager.Activate();

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        // Three ticks - enough to trip the old (buggy) 3-consecutive-mismatch rule if the read-back
        // were compared against the unclamped 20 instead of the actually-applied 35.
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(35, _fan.CurrentPercent);
        Assert.Null(alert);
        Assert.True(_settingsStore.Current.Hardware.FanControlEnabled);
    }

    // ---------------------------------------------------------------- N5: pause reason gates resume

    [Fact]
    public void ResumeFromSuspend_ClearsSystemSuspendPause()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        manager.Suspend("system suspend", resumableBySystemResume: true);
        Assert.Null(_fan.CurrentPercent);
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Null(_fan.CurrentPercent);

        manager.ResumeFromSuspend();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(40, _fan.CurrentPercent);
    }

    [Fact]
    public void ResumeFromSuspend_AfterLongPause_DoesNotImmediatelyTripWatchdog()
    {
        // R3-2: the watchdog's clock was last advanced before the pause - a real system sleep can
        // easily exceed the watchdog timeout, so without resetting the clock on resume, the very
        // first tick after resuming would look like a multi-hour stall and immediately re-trip.
        // Deterministic root-cause fix: a FakeTimeProvider drives both "now" reads and the watchdog's
        // own periodic timer, so advancing it fires the watchdog synchronously on this thread -
        // no real Thread.Sleep/wall-clock Timer, so this can never be flaky under CI load.
        EnableFixedFan(40);
        using var manager = CreateManager(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(20));
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        manager.Suspend("system suspend", resumableBySystemResume: true);

        // Simulate a "long sleep" - well past the watchdog timeout - while paused. While paused the
        // watchdog is inactive by design (N3), so this must not trip it on its own.
        _timeProvider.Advance(TimeSpan.FromMilliseconds(300));

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        manager.ResumeFromSuspend();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        // Advance a little further - well under the 80ms watchdog timeout - to prove the watchdog
        // does *not* immediately re-trip right after resuming. Advancing further with no further
        // snapshots would eventually be a real (and correctly detected) stall in its own right,
        // which is not what this test is about.
        _timeProvider.Advance(TimeSpan.FromMilliseconds(30));

        Assert.Equal(40, _fan.CurrentPercent); // resumed normally, not immediately re-paused
        Assert.Null(alert);
        Assert.True(_settingsStore.Current.Hardware.FanControlEnabled);
    }

    [Fact]
    public void ResumeFromSuspend_DoesNotClearANonResumablePause()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        // A crash-handler/session-ending pause is not resumable by PowerModes.Resume.
        manager.Suspend("unhandled exception", resumableBySystemResume: false);
        Assert.Null(_fan.CurrentPercent);

        manager.ResumeFromSuspend();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Null(_fan.CurrentPercent);
    }

    [Fact]
    public void Suspend_DoesNotDisablePersistedSetting()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();

        manager.Suspend("test suspend", resumableBySystemResume: true);

        Assert.True(_settingsStore.Current.Hardware.FanControlEnabled);
    }

    // ---------------------------------------------------------------- N3: watchdog only acts while active

    [Fact]
    public void Watchdog_NeverActivated_NeverTrips()
    {
        _settingsStore.Update(s => s.Hardware.FanProfiles[FanId] = new FanProfileSettings { Mode = FanMode.Fixed, FixedPercent = 40 });
        using var manager = CreateManager(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(20));
        // Deliberately never Activate() and never enable - control is never active.

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        // Deterministic: advancing the FakeTimeProvider fires the watchdog's periodic timer
        // synchronously on this thread, so this needs no real Thread.Sleep/wall-clock Timer.
        _timeProvider.Advance(TimeSpan.FromMilliseconds(300));

        Assert.Null(alert);
        Assert.Equal(0, _fan.SetPercentCallCount);
        Assert.Equal(0, _fan.RestoreDefaultCallCount);
    }

    [Fact]
    public void Watchdog_ActiveAndOwningButNoSnapshotForTooLong_PausesAndRestoresOwnedFans()
    {
        // Deterministic root-cause fix: the watchdog is driven by an injected FakeTimeProvider
        // (both its "now" reads and its own TimeProvider.CreateTimer-based periodic timer), so
        // advancing fake time fires it synchronously and predictably instead of relying on a real
        // Thread.Sleep racing a real System.Threading.Timer under CI load.
        EnableFixedFan(40);
        using var manager = CreateManager(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(20));
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(40, _fan.CurrentPercent);

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        // No further snapshots are raised - simulating a stuck/dead hardware thread.
        _timeProvider.Advance(TimeSpan.FromMilliseconds(400));

        Assert.Null(_fan.CurrentPercent);
        Assert.NotNull(alert);
        Assert.Equal(FanControlAlertLevel.Critical, alert!.Level);
        // N3: a stall pauses, it does not disable the persisted setting.
        Assert.True(_settingsStore.Current.Hardware.FanControlEnabled);
    }

    // ---------------------------------------------------------------- S8: activity marker

    [Fact]
    public void OnSnapshot_ActivelySettingAFan_CreatesActivityMarker()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();

        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.True(_marker.MarkerExists);
    }

    [Fact]
    public void RestoreAll_ClearsActivityMarker()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.True(_marker.MarkerExists);

        manager.RestoreAll();

        Assert.False(_marker.MarkerExists);
    }

    [Fact]
    public void Constructor_StaleMarkerFromPreviousSession_IsReportedAndCleared()
    {
        _marker.MarkerExists = true;

        using var manager = CreateManager();

        Assert.True(manager.StaleActivityMarkerDetected);
        Assert.False(_marker.MarkerExists);
    }

    // ---------------------------------------------------------------- S3-2: restore read-back verification

    [Fact]
    public void RestoreDefault_TakesEffectImmediately_NoRetryNoAlert()
    {
        // Normal case (the fake's default): RestoreDefault clears IsUnderSoftwareControl straight
        // away, so a handful of further ticks must not retry or alert at all.
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(40, _fan.CurrentPercent);

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        manager.RestoreAll();
        // Disabled so later ticks do not simply re-command the still-Fixed profile right back into
        // software mode - this test is about the restore itself never needing a retry, not about
        // whether a still-enabled profile gets re-applied (which is expected, separate behaviour).
        _settingsStore.Update(s => s.Hardware.FanControlEnabled = false);
        var restoreCallsAfterRestore = _fan.RestoreDefaultCallCount;

        for (var i = 0; i < 5; i++)
        {
            _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        }

        Assert.Equal(restoreCallsAfterRestore, _fan.RestoreDefaultCallCount);
        Assert.Null(alert);
        Assert.False(_fan.IsUnderSoftwareControl);
    }

    [Fact]
    public void RestoreDefault_StaysUnderSoftwareControl_RetriesAfterThreeTicksWithoutSetPercent()
    {
        // Known gap from PR #11: RestoreDefault() can return without throwing yet leave the channel
        // reporting software control. After enough ticks to be sure it is stuck, this must retry
        // RestoreDefault - and only RestoreDefault, never SetPercent. Control is disabled after the
        // restore so later ticks exercise *only* the verification path (VerifyPendingRestoresAndRetry
        // runs regardless of active state, before anything else) rather than the normal apply/R3-1
        // paths, which would also call RestoreDefault on their own and confuse the count.
        _fan.RestoreDefaultTakesEffect = false;
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        var setPercentCallsBeforeRestore = _fan.SetPercentCallCount;
        manager.RestoreAll();
        _settingsStore.Update(s => s.Hardware.FanControlEnabled = false);
        var restoreCallsAfterRestore = _fan.RestoreDefaultCallCount;
        Assert.True(_fan.IsUnderSoftwareControl); // the fake simulates the stuck read-back

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        // Ticks 1 and 2: still within the verification window - no retry yet.
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(restoreCallsAfterRestore, _fan.RestoreDefaultCallCount);

        // Tick 3: window elapsed while still under software control - first retry.
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(restoreCallsAfterRestore + 1, _fan.RestoreDefaultCallCount);
        Assert.Equal(setPercentCallsBeforeRestore, _fan.SetPercentCallCount); // never SetPercent
        Assert.Null(alert); // not given up yet
    }

    [Fact]
    public void RestoreDefault_NeverRecoversAfterMaxRetries_RaisesCriticalBannerAndStopsRetrying()
    {
        _fan.RestoreDefaultTakesEffect = false;
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        var setPercentCallsBeforeRestore = _fan.SetPercentCallCount;
        manager.RestoreAll();
        _settingsStore.Update(s => s.Hardware.FanControlEnabled = false);

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        // 3 verification windows of 3 ticks each cover the original call plus 3 retries (4 attempts
        // total) - enough ticks to exhaust every retry and reach the give-up banner.
        for (var i = 0; i < 12; i++)
        {
            _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        }

        Assert.NotNull(alert);
        Assert.Equal(FanControlAlertLevel.Critical, alert!.Level);
        Assert.Contains("Restart your PC", alert.Message);
        Assert.Equal(setPercentCallsBeforeRestore, _fan.SetPercentCallCount); // never SetPercent

        var restoreCallsAfterBanner = _fan.RestoreDefaultCallCount;
        alert = null;

        // Having given up, it must not keep retrying forever on further ticks.
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(restoreCallsAfterBanner, _fan.RestoreDefaultCallCount);
        Assert.Null(alert);
    }

    [Fact]
    public void RestoreDefault_RecoversPartwayThroughRetries_StopsVerifying()
    {
        _fan.RestoreDefaultTakesEffect = false;
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        manager.RestoreAll();
        _settingsStore.Update(s => s.Hardware.FanControlEnabled = false);

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        // Reach the first retry (tick 3), then let the retry actually take effect this time.
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        _fan.RestoreDefaultTakesEffect = true;
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50)); // the retry itself
        Assert.False(_fan.IsUnderSoftwareControl);
        var restoreCallsAfterRecovery = _fan.RestoreDefaultCallCount;

        for (var i = 0; i < 5; i++)
        {
            _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        }

        Assert.Equal(restoreCallsAfterRecovery, _fan.RestoreDefaultCallCount);
        Assert.Null(alert);
    }

    [Fact]
    public void FreshSetPercentAfterRestore_CancelsInFlightVerification_NeverRetriesOverIt()
    {
        // If the user switches the fan back to a Fixed/Curve profile while an earlier hand-back is
        // still being verified, the verification retry must never fight that fresh SetPercent.
        _fan.RestoreDefaultTakesEffect = false;
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        manager.RestoreAll(); // starts a verification that will never clear on its own

        // Re-enable control with a fresh fixed target before the verification window elapses.
        _settingsStore.Update(s => s.Hardware.FanProfiles[FanId] = new FanProfileSettings { Mode = FanMode.Fixed, FixedPercent = 55 });
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(55, _fan.CurrentPercent);
        var restoreCallsAfterRetake = _fan.RestoreDefaultCallCount;

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        // The engine re-commands the fixed target every tick (that is normal/expected), but the
        // cancelled restore verification must never call RestoreDefault again over it.
        for (var i = 0; i < 6; i++)
        {
            _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        }

        Assert.Equal(restoreCallsAfterRetake, _fan.RestoreDefaultCallCount);
        Assert.Equal(55, _fan.CurrentPercent);
        Assert.Null(alert);
    }
}
