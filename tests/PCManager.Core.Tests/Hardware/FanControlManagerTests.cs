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

    // ---------------------------------------------------------------- B3: unhealthy snapshot

    [Fact]
    public void OnSnapshot_ErrorStatus_ForcesFixedFanTo100()
    {
        EnableFixedFan(30);
        using var manager = CreateManager();
        manager.Activate();

        _hardwareService.RaiseSnapshot(HardwareSnapshot.Empty(HardwareStatus.Error, "boom"));

        Assert.Equal(100, _fan.CurrentPercent);
    }

    [Fact]
    public void OnSnapshot_NoCpuOrGpuTemperature_ForcesFixedFanTo100()
    {
        EnableFixedFan(30);
        using var manager = CreateManager();
        manager.Activate();

        var snapshotWithNoTemps = new HardwareSnapshot(HardwareStatus.Ready, null, [], DateTimeOffset.UtcNow);
        _hardwareService.RaiseSnapshot(snapshotWithNoTemps);

        Assert.Equal(100, _fan.CurrentPercent);
    }

    [Fact]
    public void OnSnapshot_HealthySnapshot_AppliesFixedTargetNormally()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();

        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(40, _fan.CurrentPercent);
    }

    // ---------------------------------------------------------------- rule 4: set failure

    [Fact]
    public void OnSnapshot_SetPercentThrows_RestoresAllAndDisablesSetting()
    {
        EnableFixedFan(40);
        _fan.ThrowOnSetPercent = true;
        using var manager = CreateManager();
        manager.Activate();

        FanControlAlert? alert = null;
        manager.StatusChanged += (_, e) => alert = e;

        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.False(_settingsStore.Current.Hardware.FanControlEnabled);
        Assert.True(_fan.RestoreDefaultCallCount > 0);
        Assert.NotNull(alert);
        Assert.Equal(FanControlAlertLevel.Critical, alert!.Level);
    }

    [Fact]
    public void OnSnapshot_SetPercentKeepsThrowing_OnlyAlertsOnce()
    {
        EnableFixedFan(40);
        _fan.ThrowOnSetPercent = true;
        using var manager = CreateManager();
        manager.Activate();

        var alertCount = 0;
        manager.StatusChanged += (_, _) => alertCount++;

        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        // Re-enable is required for the engine to attempt SetPercent again (otherwise it is already
        // disabled and just restores) - simulate a caller re-arming and failing again.
        manager.Rearm();
        _settingsStore.Update(s => s.Hardware.FanControlEnabled = true);
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(2, alertCount);
    }

    // ---------------------------------------------------------------- B2: suspend/resume

    [Fact]
    public void Suspend_RestoresImmediately_AndStaysPausedUntilResume()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Equal(40, _fan.CurrentPercent);

        manager.Suspend("test suspend");

        Assert.Null(_fan.CurrentPercent);

        // Even though settings still say enabled and the fan is still armed, a snapshot arriving
        // while paused must not re-apply the fixed target - this is exactly what B2 flagged as
        // "undone within one tick".
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));
        Assert.Null(_fan.CurrentPercent);

        manager.ResumeFromSuspend();
        _hardwareService.RaiseSnapshot(HealthySnapshotWithCpuTemp(50));

        Assert.Equal(40, _fan.CurrentPercent);
    }

    [Fact]
    public void Suspend_DoesNotDisablePersistedSetting()
    {
        EnableFixedFan(40);
        using var manager = CreateManager();
        manager.Activate();

        manager.Suspend("test suspend");

        Assert.True(_settingsStore.Current.Hardware.FanControlEnabled);
    }

    // ---------------------------------------------------------------- watchdog

    [Fact]
    public void Watchdog_NoSnapshotForTooLong_RestoresAllAndAlerts()
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
        Assert.False(_settingsStore.Current.Hardware.FanControlEnabled);
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
