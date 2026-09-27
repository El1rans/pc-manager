using Microsoft.Extensions.Logging.Abstractions;
using PCManager.Core.Hardware;
using PCManager.Core.Settings;
using Xunit;

namespace PCManager.Core.Tests.Hardware;

public sealed class FanControlManagerTests : IDisposable
{
    private const string FanId = "fan1";

    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeHardwareService _hardwareService = new();
    private readonly FakeFanController _fan = new(FanId);
    private readonly FakeFanControlActivityMarker _marker = new();
    private readonly FanControlEngine _engine = new(new FakeClock());

    public FanControlManagerTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PCManagerTests_" + Guid.NewGuid().ToString("N"));
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
            watchdogPollInterval ?? TimeSpan.FromMinutes(10));

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
    public void OnSnapshot_NotElevatedWithSavedFixedProfile_ArmedAndEnabled_StillAppliesNoTempFailsafe()
    {
        // Unlike the master-off/never-activated cases above, once the user HAS armed and enabled
        // control, a NotElevated (no CPU temperature visible) snapshot is exactly rule 3's "no
        // trustworthy temperature" case and correctly forces the owned fan to 100% - the N1 bug was
        // that this fired even when control was never armed/enabled in the first place, not that it
        // should never fire at all once armed.
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();

        _hardwareService.RaiseSnapshot(HardwareSnapshot.Empty(HardwareStatus.NotElevated));

        Assert.Equal(100, _fan.CurrentPercent);
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

        Thread.Sleep(TimeSpan.FromMilliseconds(300));

        Assert.Null(alert);
        Assert.Equal(0, _fan.SetPercentCallCount);
        Assert.Equal(0, _fan.RestoreDefaultCallCount);
    }

    [Fact]
    public void Watchdog_ActiveAndOwningButNoSnapshotForTooLong_PausesAndRestoresOwnedFans()
    {
        EnableFixedFan(40);
        using var manager = CreateManager(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(20));
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(40, _fan.CurrentPercent);

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        // No further snapshots are raised - simulating a stuck/dead hardware thread.
        Thread.Sleep(TimeSpan.FromMilliseconds(400));

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
}
