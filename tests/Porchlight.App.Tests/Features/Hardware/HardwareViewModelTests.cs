using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Controls;
using Porchlight.App.Features.Hardware;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.App.Tests.Features.Setup;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Components;
using Porchlight.Core.Hardware;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Features.Hardware;

public sealed class HardwareViewModelTests
{
    private const string ConflictWarningSuffix =
        " also controlling your fans. Porchlight's settings may be overridden. Close/disable it to use Porchlight fan control.";

    private static Harness CreateHarness(
        HardwareStatus status = HardwareStatus.NotElevated,
        IReadOnlyList<HardwareNode>? nodes = null,
        IReadOnlyList<IFanController>? controllers = null,
        Action<HardwareSettings>? configureSettings = null,
        bool staleActivityMarker = false) =>
        new(status, nodes ?? [], controllers ?? [], configureSettings, staleActivityMarker);

    private static HardwareSnapshot Snapshot(HardwareStatus status, params HardwareNode[] nodes) =>
        new(status, null, nodes, DateTimeOffset.UtcNow);

    private static SensorReading Sensor(string id, string name, SensorType type, double value) =>
        new(id, name, type, value, value, value, DateTimeOffset.UtcNow);

    private static HardwareNode Node(string id, string name, HardwareNodeType type, params SensorReading[] sensors) =>
        new(id, name, type, sensors, []);

    /// <summary>Runs everything already queued on this thread's dispatcher (the view model marshals
    /// snapshot ticks and fan-control alerts onto it) - tests have no running WPF Application.</summary>
    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.InvokeAsync(() => frame.Continue = false, DispatcherPriority.ApplicationIdle);
        Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrowAndDisposesOwnedPawnIoCard()
    {
        using var harness = CreateHarness();
        Assert.Equal(1, harness.Components.StatusChangedSubscriberCount);

        harness.ViewModel.Dispose();
        var exception = Record.Exception(harness.ViewModel.Dispose);

        Assert.Null(exception);
        // The PawnIO card was created by the view model through the factory, so the view model
        // (not the container) owns disposing it - and a disposed card unsubscribes.
        Assert.Equal(0, harness.Components.StatusChangedSubscriberCount);
    }

    [Fact]
    public void Dispose_StopsListeningToHardwareSnapshots()
    {
        using var harness = CreateHarness();
        var subscribersWhileAlive = harness.Hardware.SnapshotSubscriberCount;

        harness.ViewModel.Dispose();

        Assert.Equal(subscribersWhileAlive - 1, harness.Hardware.SnapshotSubscriberCount);
    }

    [Fact]
    public void Constructor_AfterAnUncleanShutdown_TellsTheUserToRestartTheirPc()
    {
        using var harness = CreateHarness(staleActivityMarker: true);

        Assert.Contains("Restart your PC", harness.ViewModel.CriticalMessage);
    }

    [Theory]
    [InlineData(5, 50, 20, 70)] // below the lowest allowed floor / failsafe (e.g. a hand-edited settings.json)
    [InlineData(40, 99, 40, 95)] // above the highest allowed failsafe
    [InlineData(40, 80, 40, 80)] // already valid: untouched
    public void Constructor_ClampsFanControlLimitsReadFromSettings(
        int storedMinPercent, double storedFailsafeC, int expectedMinPercent, double expectedFailsafeC)
    {
        using var harness = CreateHarness(configureSettings: s =>
        {
            s.MinFanPercent = storedMinPercent;
            s.FailsafeTemperatureC = storedFailsafeC;
        });

        Assert.Equal(expectedMinPercent, harness.ViewModel.MinFanPercent);
        Assert.Equal(expectedFailsafeC, harness.ViewModel.FailsafeTemperatureC);
    }

    [Theory]
    [InlineData(HardwareStatus.NotElevated, false, "Restart as administrator to use software fan control.")]
    [InlineData(HardwareStatus.DriverMissing, false, "Install the PawnIO driver above to use software fan control.")]
    [InlineData(HardwareStatus.Error, false, "Fan control is unavailable while the hardware monitor has an error.")]
    [InlineData(HardwareStatus.Ready, true, "")]
    public void FanControlToggle_AvailabilityAndReason_FollowTheHardwareStatus(
        HardwareStatus status, bool expectedCanToggle, string expectedReason)
    {
        using var harness = CreateHarness(status);

        Assert.Equal(expectedCanToggle, harness.ViewModel.CanToggleSoftwareFanControl);
        Assert.Equal(expectedReason, harness.ViewModel.SoftwareFanControlDisabledReason);
    }

    [Theory]
    [InlineData(ComponentState.NotInstalled, true)]
    [InlineData(ComponentState.Error, true)]
    [InlineData(ComponentState.Installed, false)]
    [InlineData(ComponentState.Running, false)]
    public void ShowDriverCard_IsTrueUntilPawnIoIsInstalled(ComponentState state, bool expected)
    {
        using var harness = CreateHarness();

        harness.ViewModel.PawnIoCard.Status = new ComponentStatus(state);

        Assert.Equal(expected, harness.ViewModel.ShowDriverCard);
    }

    [Fact]
    public void PawnIoStatusChange_NotifiesShowDriverCard()
    {
        using var harness = CreateHarness();
        var changed = new List<string?>();
        harness.ViewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        harness.ViewModel.PawnIoCard.Status = new ComponentStatus(ComponentState.Installed);

        Assert.Contains(nameof(HardwareViewModel.ShowDriverCard), changed);
    }

    [Theory]
    [InlineData(HardwareStatus.Ready, false, true)] // initialized but nothing controllable: say so
    [InlineData(HardwareStatus.Ready, true, false)]
    [InlineData(HardwareStatus.NotElevated, false, false)] // not "no fans" - just not loaded/allowed yet
    public void ShowNoControllableFansMessage_OnlyWhenReadyAndNoFanWasFound(HardwareStatus status, bool hasFan, bool expected)
    {
        using var harness = CreateHarness(status, controllers: hasFan ? [new FakeFanController("ctl-1")] : []);

        Assert.Equal(expected, harness.ViewModel.ShowNoControllableFansMessage);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Armoury Crate", "Armoury Crate is")]
    [InlineData("MSI Center|iCUE", "MSI Center, iCUE are")]
    public async Task RefreshConflictDetection_BuildsTheWarningFromTheDetectedSoftware(string detected, string expectedLead)
    {
        using var harness = CreateHarness();
        harness.Conflicts.Detected = detected.Length == 0 ? [] : detected.Split('|');

        await harness.ViewModel.RefreshConflictDetectionCommand.ExecuteAsync(null);

        Assert.Equal(expectedLead.Length > 0, harness.ViewModel.ShowConflictWarning);
        Assert.Equal(expectedLead.Length > 0 ? expectedLead + ConflictWarningSuffix : string.Empty, harness.ViewModel.ConflictWarningMessage);
    }

    [Fact]
    public async Task RefreshConflictDetection_WhenTheDetectorThrows_KeepsThePreviousWarningAndDoesNotThrow()
    {
        using var harness = CreateHarness();
        harness.Conflicts.Detected = ["iCUE"];
        await harness.ViewModel.RefreshConflictDetectionCommand.ExecuteAsync(null);
        harness.Conflicts.ThrowOnDetect = true;

        var exception = await Record.ExceptionAsync(() => harness.ViewModel.RefreshConflictDetectionCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.True(harness.ViewModel.ShowConflictWarning);
    }

    [Fact]
    public async Task OnNavigatedTo_ArmsFanControlOnTheHardwareThread_WithoutFallingBackToADirectCall()
    {
        using var harness = CreateHarness();

        await harness.ViewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        // Arming must never run twice (once now, once when a queued copy is eventually picked up).
        Assert.Equal([false], harness.Hardware.OwnerThreadCallFallbackFlags);
    }

    [Theory]
    [InlineData(true)] // navigating to the page
    [InlineData(false)] // selecting the Fans tab later
    public void ConflictDetection_RunsWhenThePageOpensAndWhenTheFansTabIsSelected(bool viaNavigation)
    {
        using var harness = CreateHarness();

        if (viaNavigation)
        {
            _ = harness.ViewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            harness.ViewModel.OnFansTabSelected();
        }

        Assert.True(harness.Conflicts.DetectCalled.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ResetMinMaxCommand_AsksTheHardwareServiceToResetSensorExtremes()
    {
        using var harness = CreateHarness();

        harness.ViewModel.ResetMinMaxCommand.Execute(null);

        Assert.Equal(1, harness.Hardware.ResetMinMaxCallCount);
    }

    [Theory]
    [InlineData(true)] // the "Restore all fans" button
    [InlineData(false)] // flipping the master switch off
    public void TurningSoftwareFanControlOff_PersistsItAndHandsEveryFanBackToBios(bool viaRestoreAllCommand)
    {
        using var harness = CreateHarness(HardwareStatus.Ready, configureSettings: s => s.FanControlEnabled = true);
        Assert.True(harness.ViewModel.SoftwareFanControlEnabled);

        if (viaRestoreAllCommand)
        {
            harness.ViewModel.RestoreAllFansCommand.Execute(null);
        }
        else
        {
            harness.ViewModel.SoftwareFanControlEnabled = false;
        }

        Assert.False(harness.ViewModel.SoftwareFanControlEnabled);
        Assert.False(harness.Settings.Current.Hardware.FanControlEnabled);
        // RestoreAll is a safety action: it may fall back to a direct call if the hardware thread is stuck.
        Assert.Equal([true], harness.Hardware.OwnerThreadCallFallbackFlags);
    }

    [Theory]
    [InlineData(HardwareStatus.NotElevated)]
    [InlineData(HardwareStatus.DriverMissing)]
    [InlineData(HardwareStatus.Error)]
    public void TurningSoftwareFanControlOn_WhileHardwareIsNotReady_RevertsTheToggleAndChangesNothing(HardwareStatus status)
    {
        using var harness = CreateHarness(status);

        harness.ViewModel.SoftwareFanControlEnabled = true;

        Assert.False(harness.ViewModel.SoftwareFanControlEnabled);
        Assert.False(harness.Settings.Current.Hardware.FanControlEnabled);
        Assert.Empty(harness.Hardware.OwnerThreadCallFallbackFlags);
    }

    [Fact]
    public void TurningSoftwareFanControlOn_AfterTheRiskWarningWasConfirmed_PersistsRearmsAndClearsTheCriticalBanner()
    {
        using var harness = CreateHarness(
            HardwareStatus.Ready, configureSettings: s => s.FanControlWarningConfirmed = true, staleActivityMarker: true);
        Assert.NotNull(harness.ViewModel.CriticalMessage);

        harness.ViewModel.SoftwareFanControlEnabled = true;

        Assert.True(harness.ViewModel.SoftwareFanControlEnabled);
        Assert.True(harness.Settings.Current.Hardware.FanControlEnabled);
        Assert.Null(harness.ViewModel.CriticalMessage);
        // Re-arming must never run twice (queued copy plus a direct fallback), unlike RestoreAll.
        Assert.Equal([false], harness.Hardware.OwnerThreadCallFallbackFlags);
    }

    [Theory]
    [InlineData(5, 20)] // never below the lowest allowed floor
    [InlineData(45, 45)]
    public void MinFanPercent_IsClampedPersistedAndPushedToEveryFanCard(int requested, int expected)
    {
        using var harness = CreateHarness(HardwareStatus.Ready, controllers: [new FakeFanController("ctl-1")]);

        harness.ViewModel.MinFanPercent = requested;

        Assert.Equal(expected, harness.ViewModel.MinFanPercent);
        Assert.Equal(expected, harness.Settings.Current.Hardware.MinFanPercent);
        Assert.Equal(expected, Assert.Single(harness.ViewModel.Fans).MinFanPercent);
    }

    [Theory]
    [InlineData(60, 70)] // below the lowest allowed overheat failsafe
    [InlineData(100, 95)] // above the highest allowed one
    [InlineData(80, 80)]
    public void FailsafeTemperatureC_IsClampedAndPersisted(double requested, double expected)
    {
        using var harness = CreateHarness();

        harness.ViewModel.FailsafeTemperatureC = requested;

        Assert.Equal(expected, harness.ViewModel.FailsafeTemperatureC);
        Assert.Equal(expected, harness.Settings.Current.Hardware.FailsafeTemperatureC);
    }

    [Fact]
    public void HideUnusedSensors_PersistsAndHidesSensorsThatNeverReportedARealValue()
    {
        var board = Node("board", "Board", HardwareNodeType.Motherboard, Sensor("rpm-1", "Fan #1", SensorType.Fan, 0));
        using var harness = CreateHarness(HardwareStatus.Ready, [board], configureSettings: s => s.HideUnusedSensors = false);
        var row = harness.ViewModel.Cards[0].Sections[0].Sensors[0];
        Assert.True(row.IsVisible);

        harness.ViewModel.HideUnusedSensors = true;

        Assert.True(harness.Settings.Current.Hardware.HideUnusedSensors);
        Assert.False(row.IsVisible);
    }

    [Fact]
    public void HideUnusedSensors_UpdatesFanCardVisibilityWithoutWaitingForTheNextTick()
    {
        var board = Node("board", "Board", HardwareNodeType.Motherboard, Sensor("rpm-1", "Fan #1", SensorType.Fan, 0));
        using var harness = CreateHarness(
            HardwareStatus.Ready, [board], [new FakeFanController("ctl-1", rpmSensorId: "rpm-1")],
            configureSettings: s => s.HideUnusedSensors = false);
        var fan = Assert.Single(harness.ViewModel.Fans);
        Assert.True(fan.IsVisible);

        harness.ViewModel.HideUnusedSensors = true;

        Assert.False(fan.IsVisible);
    }

    [Fact]
    public void FilterText_IsTrimmedAndAppliedToEveryCard()
    {
        var cpu = Node("cpu", "Intel CPU", HardwareNodeType.Cpu, Sensor("t1", "CPU Package", SensorType.Temperature, 50));
        var gpu = Node("gpu", "NVIDIA GPU", HardwareNodeType.Gpu, Sensor("t2", "GPU Core", SensorType.Temperature, 60));
        using var harness = CreateHarness(HardwareStatus.Ready, [cpu, gpu]);

        harness.ViewModel.FilterText = "  nvidia ";

        Assert.Equal(["gpu"], harness.ViewModel.Cards.Where(c => c.IsVisible).Select(c => c.Id));
    }

    [Fact]
    public void SnapshotArrivingWhileTheViewIsInactive_IsOnlyAppliedOnceTheViewBecomesActive()
    {
        using var harness = CreateHarness();

        harness.Hardware.Raise(Snapshot(HardwareStatus.Ready, Node("cpu", "Intel CPU", HardwareNodeType.Cpu)));
        PumpDispatcher();
        Assert.Equal(HardwareStatus.NotElevated, harness.ViewModel.Status);
        Assert.Empty(harness.ViewModel.Cards);

        harness.ViewModel.SetViewActive(true);

        Assert.Equal(HardwareStatus.Ready, harness.ViewModel.Status);
        Assert.Single(harness.ViewModel.Cards);
    }

    [Fact]
    public void SnapshotArrivingWhileTheViewIsActive_IsAppliedOnTheDispatcher()
    {
        using var harness = CreateHarness();
        harness.ViewModel.SetViewActive(true);

        harness.Hardware.Raise(Snapshot(HardwareStatus.Ready, Node("cpu", "Intel CPU", HardwareNodeType.Cpu)));
        PumpDispatcher();

        Assert.Equal(HardwareStatus.Ready, harness.ViewModel.Status);
        Assert.Single(harness.ViewModel.Cards);
    }

    [Fact]
    public void Cards_AreOrderedCpuGpuMotherboardMemoryStorageNetworkThenAnythingElse()
    {
        var nodes = new[]
        {
            Node("net", "Ethernet", HardwareNodeType.Network),
            Node("disk", "SSD", HardwareNodeType.Storage),
            Node("other", "Gadget", HardwareNodeType.Other),
            Node("gpu", "GPU", HardwareNodeType.Gpu),
            Node("cpu", "CPU", HardwareNodeType.Cpu),
        };

        using var harness = CreateHarness(HardwareStatus.Ready, nodes);

        Assert.Equal(["cpu", "gpu", "disk", "net", "other"], harness.ViewModel.Cards.Select(c => c.Id));
    }

    [Fact]
    public void Cards_AreMergedInPlace_KeepingExistingInstancesAndDroppingVanishedDevices()
    {
        var gpu = Node("gpu", "GPU", HardwareNodeType.Gpu);
        var disk = Node("disk", "SSD", HardwareNodeType.Storage);
        using var harness = CreateHarness(HardwareStatus.Ready, [gpu, disk]);
        var gpuCard = harness.ViewModel.Cards[0];
        harness.ViewModel.SetViewActive(true);

        // A CPU appears and the SSD vanishes: the CPU card slots in first, the GPU card is the same
        // instance (so its expanded state survives), and the SSD card is gone.
        harness.Hardware.Raise(Snapshot(HardwareStatus.Ready, Node("cpu", "CPU", HardwareNodeType.Cpu), gpu));
        PumpDispatcher();

        Assert.Equal(["cpu", "gpu"], harness.ViewModel.Cards.Select(c => c.Id));
        Assert.Same(gpuCard, harness.ViewModel.Cards[1]);
    }

    [Fact]
    public void SummaryTiles_AppearAndDisappearWithTheReadingsTheyAreBuiltFrom()
    {
        var cpu = Node("cpu", "CPU", HardwareNodeType.Cpu, Sensor("t", "CPU Package", SensorType.Temperature, 60));
        using var harness = CreateHarness(HardwareStatus.Ready, [cpu]);
        var tile = Assert.Single(harness.ViewModel.SummaryTiles);
        Assert.Equal("CPU temperature", tile.Title);
        harness.ViewModel.SetViewActive(true);

        harness.Hardware.Raise(Snapshot(HardwareStatus.Ready));
        PumpDispatcher();

        Assert.Empty(harness.ViewModel.SummaryTiles);
    }

    [Fact]
    public void Fans_FollowTheControllersAndShowTheirPairedRpmReading()
    {
        var board = Node("board", "Board", HardwareNodeType.Motherboard, Sensor("rpm-1", "Fan #1", SensorType.Fan, 900));
        var first = new FakeFanController("ctl-1", rpmSensorId: "rpm-1") { CurrentPercent = 40 };
        var second = new FakeFanController("ctl-2");
        using var harness = CreateHarness(HardwareStatus.Ready, [board], [first, second]);
        harness.ViewModel.SetViewActive(true);
        var firstCard = harness.ViewModel.Fans[0];
        Assert.Equal(["ctl-1", "ctl-2"], harness.ViewModel.Fans.Select(f => f.FanId));
        Assert.Equal("900 RPM", firstCard.RpmDisplayText);
        Assert.Equal(40, firstCard.CurrentPercent);

        harness.Hardware.ControllerList.Remove(second);
        harness.Hardware.Raise(Snapshot(HardwareStatus.Ready, board));
        PumpDispatcher();

        Assert.Same(firstCard, Assert.Single(harness.ViewModel.Fans));
    }

    [Fact]
    public void Fans_OfferOnlyRealTemperatureSensorsAsCurveSources_NeverAnInvertedOne()
    {
        var cpu = Node(
            "cpu", "CPU", HardwareNodeType.Cpu,
            Sensor("t1", "CPU Package", SensorType.Temperature, 50),
            Sensor("t2", "Core #0 Distance to TjMax", SensorType.Temperature, 45), // would make a curve react backwards
            Sensor("load", "CPU Total", SensorType.Load, 10));
        using var harness = CreateHarness(HardwareStatus.Ready, [cpu], [new FakeFanController("ctl-1")]);

        var fan = Assert.Single(harness.ViewModel.Fans);

        Assert.Equal(["t1"], fan.TemperatureSensorOptions.Select(o => o.Id));
    }

    [Fact]
    public void Fans_CustomName_IsShownOnThatFansRpmSensorRow()
    {
        var board = Node("board", "Board", HardwareNodeType.Motherboard, Sensor("rpm-1", "Fan #1", SensorType.Fan, 900));
        using var harness = CreateHarness(
            HardwareStatus.Ready, [board], [new FakeFanController("ctl-1", rpmSensorId: "rpm-1")],
            configureSettings: s => s.FanDisplayNames["ctl-1"] = "Front intake");

        var row = harness.ViewModel.Cards[0].Sections[0].Sensors[0];

        // The name is saved against the controller id, but the Sensors tab row is keyed by RPM sensor id.
        Assert.Equal("Front intake", row.Name);
    }

    [Theory]
    [InlineData(HardwareStatus.Ready, true, true)]
    [InlineData(HardwareStatus.Ready, false, false)] // master switch off: the selector is not editable
    [InlineData(HardwareStatus.NotElevated, true, false)] // switch persisted on, but hardware access is gone
    public void Fans_ModeIsEditableOnlyWhileFanControlIsAvailableAndSwitchedOn(HardwareStatus status, bool switchedOn, bool expected)
    {
        using var harness = CreateHarness(
            status, controllers: [new FakeFanController("ctl-1")], configureSettings: s => s.FanControlEnabled = switchedOn);

        Assert.Equal(expected, Assert.Single(harness.ViewModel.Fans).CanEditMode);
    }

    [Theory]
    [InlineData(0, false)] // an unconnected header that never spun is hidden ("hide unused" defaults on)
    [InlineData(1200, true)]
    [InlineData(-1, true)] // a fan with no paired RPM sensor has no history to judge it by, so it stays
    public void Fans_AreHiddenOnlyWhenTheirRpmSensorNeverReportedARealValue(double rpm, bool expectedVisible)
    {
        var board = Node("board", "Board", HardwareNodeType.Motherboard, Sensor("rpm-1", "Fan #1", SensorType.Fan, Math.Max(rpm, 0)));
        using var harness = CreateHarness(
            HardwareStatus.Ready, [board], [new FakeFanController("ctl-1", rpmSensorId: rpm < 0 ? null : "rpm-1")]);

        Assert.Equal(expectedVisible, Assert.Single(harness.ViewModel.Fans).IsVisible);
    }

    [Fact]
    public void CriticalFanControlAlert_ShowsItsMessage_AndSwitchesTheToggleOffToMatchTheDisabledSetting()
    {
        using var harness = CreateHarness(
            HardwareStatus.Ready, controllers: [new FakeFanController("ctl-1")], configureSettings: s => s.FanControlEnabled = true);
        harness.Manager.Activate();
        Assert.True(harness.ViewModel.SoftwareFanControlEnabled);

        // An unreadable hardware tick while control is active is a rule-4 failure: the manager
        // turns the persisted setting off and raises a critical alert.
        harness.Hardware.Raise(Snapshot(HardwareStatus.Error));
        PumpDispatcher();

        Assert.StartsWith("Could not read hardware sensors", harness.ViewModel.CriticalMessage);
        Assert.False(harness.Settings.Current.Hardware.FanControlEnabled);
        Assert.False(harness.ViewModel.SoftwareFanControlEnabled);
    }

    [Fact]
    public void CautionFanControlAlert_DoesNotShowTheCriticalBanner()
    {
        using var harness = CreateHarness(HardwareStatus.Ready);

        harness.Manager.Suspend("system suspend", resumableBySystemResume: true); // raises a Caution alert
        PumpDispatcher();

        Assert.Null(harness.ViewModel.CriticalMessage);
    }

    /// <summary>Everything a <see cref="HardwareViewModel"/> needs, wired to fakes. The view model is
    /// built last so the initial snapshot, controllers and settings are already in place.</summary>
    private sealed class Harness : IDisposable
    {
        public Harness(
            HardwareStatus status,
            IReadOnlyList<HardwareNode> nodes,
            IReadOnlyList<IFanController> controllers,
            Action<HardwareSettings>? configureSettings,
            bool staleActivityMarker)
        {
            configureSettings?.Invoke(Settings.Current.Hardware);
            Hardware.ControllerList.AddRange(controllers);
            Hardware.Latest = new HardwareSnapshot(status, null, nodes, DateTimeOffset.UtcNow);
            Manager = new FanControlManager(
                Hardware, Settings, new FanControlEngine(new FakeClock()), new FakeActivityMarker(staleActivityMarker),
                NullLogger<FanControlManager>.Instance);
            ViewModel = new HardwareViewModel(
                Hardware, Manager, Settings, new FakeElevationService(),
                new ComponentCardViewModelFactory(Components, NullLoggerFactory.Instance),
                Conflicts,
                NullLogger<HardwareViewModel>.Instance,
                NullLogger<FanCardViewModel>.Instance);
        }

        public FakeHardwareService Hardware { get; } = new();

        public FakeSettingsStore Settings { get; } = new();

        public FakeComponentService Components { get; } = new();

        public FakeFanControlConflictDetector Conflicts { get; } = new();

        public FanControlManager Manager { get; }

        public HardwareViewModel ViewModel { get; }

        public void Dispose()
        {
            ViewModel.Dispose();
            Manager.Dispose();
        }
    }

    private sealed class FakeHardwareService : IHardwareService
    {
        public event EventHandler<HardwareSnapshot>? SnapshotUpdated;

        public int SnapshotSubscriberCount => SnapshotUpdated?.GetInvocationList().Length ?? 0;

        public HardwareSnapshot Latest { get; set; } = HardwareSnapshot.Empty(HardwareStatus.NotElevated);

        public List<IFanController> ControllerList { get; } = [];

        IReadOnlyList<IFanController> IHardwareService.Controllers => ControllerList;

        public int ResetMinMaxCallCount { get; private set; }

        /// <summary>The <c>allowDirectFallback</c> argument of every <see cref="RunOnOwnerThread"/>
        /// call: true marks a safety action (restore), false a command that must never run twice.</summary>
        public List<bool> OwnerThreadCallFallbackFlags { get; } = [];

        public void Raise(HardwareSnapshot snapshot)
        {
            Latest = snapshot;
            SnapshotUpdated?.Invoke(this, snapshot);
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void ResetMinMax() => ResetMinMaxCallCount++;

        public void RunOnOwnerThread(Action action, TimeSpan timeout, bool allowDirectFallback = true)
        {
            OwnerThreadCallFallbackFlags.Add(allowDirectFallback);
            action();
        }
    }

    private sealed class FakeFanController(string id, string? rpmSensorId = null) : IFanController
    {
        public string Id { get; } = id;

        public string Name => $"Fan {Id}";

        public HardwareNodeType NodeType => HardwareNodeType.Motherboard;

        public double? CurrentPercent { get; set; }

        public bool IsUnderSoftwareControl => false;

        public bool CanControl => true;

        public string? RpmSensorId { get; } = rpmSensorId;

        public double MinSoftwarePercent => 0;

        public double MaxSoftwarePercent => 100;

        public void SetPercent(double percent) => CurrentPercent = percent;

        public void RestoreDefault() => CurrentPercent = null;
    }

    private sealed class FakeActivityMarker(bool exists) : IFanControlActivityMarker
    {
        public bool Exists() => exists;

        public void Create()
        {
        }

        public void Delete()
        {
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private sealed class FakeFanControlConflictDetector : IFanControlConflictDetector
    {
        public IReadOnlyList<string> Detected { get; set; } = [];

        public bool ThrowOnDetect { get; set; }

        public ManualResetEventSlim DetectCalled { get; } = new();

        public IReadOnlyList<string> DetectConflicts()
        {
            DetectCalled.Set();
            return ThrowOnDetect ? throw new InvalidOperationException("Process enumeration failed.") : Detected;
        }
    }
}
