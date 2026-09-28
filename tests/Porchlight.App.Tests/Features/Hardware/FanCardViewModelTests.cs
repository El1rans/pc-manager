using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Hardware;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.App.Tests.Features.Hardware;

/// <summary>
/// Spec 04 addendum (fans polish): hiding an empty motherboard fan header, never hiding a GPU fan,
/// and showing "Stopped (idle)" for a GPU fan at 0 RPM.
/// </summary>
public sealed class FanCardViewModelTests
{
    private static FanCardViewModel CreateCard(string id, HardwareNodeType nodeType) =>
        new(id, "Fan", nodeType, new FakeSettingsStore(), NullLogger<FanCardViewModel>.Instance);

    [Fact]
    public void UpdateVisibility_MotherboardFan_NeverReportedRpm_HiddenWhenHideUnusedSensorsOn()
    {
        var card = CreateCard("fan-1", HardwareNodeType.Motherboard);

        card.UpdateVisibility(hideUnusedSensors: true, everReportedRpm: false);

        Assert.False(card.IsVisible);
    }

    [Fact]
    public void UpdateVisibility_MotherboardFan_NeverReportedRpm_VisibleWhenHideUnusedSensorsOff()
    {
        var card = CreateCard("fan-1", HardwareNodeType.Motherboard);

        card.UpdateVisibility(hideUnusedSensors: false, everReportedRpm: false);

        Assert.True(card.IsVisible);
    }

    [Fact]
    public void UpdateVisibility_MotherboardFan_EverReportedRpm_StaysVisible_EvenIfNowZero()
    {
        var card = CreateCard("fan-1", HardwareNodeType.Motherboard);

        // Once real, always visible (spec 10's "hide unused" rule, reused here) - even though the
        // most recent tick's own RPM has since idled back to 0, the *history* is what is passed in.
        card.UpdateVisibility(hideUnusedSensors: true, everReportedRpm: true);

        Assert.True(card.IsVisible);
    }

    [Fact]
    public void UpdateVisibility_GpuFan_NeverReportedRpm_StaysVisibleRegardlessOfHideUnusedSensors()
    {
        var card = CreateCard("gpu-fan-1", HardwareNodeType.Gpu);

        card.UpdateVisibility(hideUnusedSensors: true, everReportedRpm: false);

        Assert.True(card.IsVisible);
    }

    [Fact]
    public void UpdateHideUnusedSensorsSetting_RecomputesFromCachedHistory_WithoutANewReading()
    {
        var card = CreateCard("fan-1", HardwareNodeType.Motherboard);
        card.UpdateVisibility(hideUnusedSensors: false, everReportedRpm: false);
        Assert.True(card.IsVisible);

        // The toggle flips before the next hardware tick arrives - must recompute immediately from
        // the cached RPM history rather than waiting for another UpdateVisibility call.
        card.UpdateHideUnusedSensorsSetting(true);

        Assert.False(card.IsVisible);
    }

    [Fact]
    public void Rename_CommitsCustomName_PersistsKeyedByStableFanId_AndDisplaysItEverywhere()
    {
        var settingsStore = new FakeSettingsStore();
        var card = new FanCardViewModel("fan-1", "CPU fan", HardwareNodeType.Motherboard, settingsStore, NullLogger<FanCardViewModel>.Instance);

        card.BeginRenamingCommand.Execute(null);
        card.NameEditText = "Front intake";
        card.CommitRenameCommand.Execute(null);

        Assert.False(card.IsEditingName);
        Assert.Equal("Front intake", card.DisplayName);
        Assert.True(card.HasCustomName);
        Assert.Equal("CPU fan", card.Name); // original hardware name still available as the subtitle
        Assert.Equal("Front intake", settingsStore.Current.Hardware.FanDisplayNames["fan-1"]);
    }

    [Fact]
    public void Rename_ClearingCustomName_RemovesSettingsEntry_AndFallsBackToHardwareName()
    {
        var settingsStore = new FakeSettingsStore();
        settingsStore.Current.Hardware.FanDisplayNames["fan-1"] = "Front intake";
        var card = new FanCardViewModel("fan-1", "CPU fan", HardwareNodeType.Motherboard, settingsStore, NullLogger<FanCardViewModel>.Instance);
        Assert.Equal("Front intake", card.DisplayName);

        card.BeginRenamingCommand.Execute(null);
        card.NameEditText = string.Empty;
        card.CommitRenameCommand.Execute(null);

        Assert.Equal("CPU fan", card.DisplayName);
        Assert.False(card.HasCustomName);
        Assert.False(settingsStore.Current.Hardware.FanDisplayNames.ContainsKey("fan-1"));
    }

    [Fact]
    public void Constructor_LoadsPreviouslyPersistedCustomName_SurvivingAcrossInstances()
    {
        // Simulates a reload: the first instance persists a rename, a second instance built later
        // from the same settings store (as happens on the next app launch) picks it up.
        var settingsStore = new FakeSettingsStore();
        var first = new FanCardViewModel("fan-1", "CPU fan", HardwareNodeType.Motherboard, settingsStore, NullLogger<FanCardViewModel>.Instance);
        first.BeginRenamingCommand.Execute(null);
        first.NameEditText = "Front intake";
        first.CommitRenameCommand.Execute(null);

        var second = new FanCardViewModel("fan-1", "CPU fan", HardwareNodeType.Motherboard, settingsStore, NullLogger<FanCardViewModel>.Instance);

        Assert.Equal("Front intake", second.DisplayName);
    }

    [Fact]
    public void CancelRenaming_DiscardsEditWithoutPersisting()
    {
        var settingsStore = new FakeSettingsStore();
        var card = new FanCardViewModel("fan-1", "CPU fan", HardwareNodeType.Motherboard, settingsStore, NullLogger<FanCardViewModel>.Instance);

        card.BeginRenamingCommand.Execute(null);
        card.NameEditText = "Discarded name";
        card.CancelRenamingCommand.Execute(null);

        Assert.False(card.IsEditingName);
        Assert.Equal("CPU fan", card.DisplayName);
        Assert.False(settingsStore.Current.Hardware.FanDisplayNames.ContainsKey("fan-1"));
    }

    [Fact]
    public void UpdateReadings_GpuFan_ZeroRpm_ShowsStoppedIdle_NotZeroRpm()
    {
        var card = CreateCard("gpu-fan-1", HardwareNodeType.Gpu);
        var controller = new FakeFanController("gpu-fan-1", HardwareNodeType.Gpu) { CurrentPercent = 30 };
        var rpmSensor = new SensorReading("gpu-fan-1-rpm", "GPU fan", SensorType.Fan, 0, 0, 0, DateTimeOffset.UtcNow);

        card.UpdateReadings(controller, rpmSensor);

        Assert.Equal("Stopped (idle)", card.RpmDisplayText);
    }

    [Fact]
    public void UpdateReadings_MotherboardFan_ZeroRpm_ShowsZeroRpm_NotStoppedIdle()
    {
        var card = CreateCard("fan-1", HardwareNodeType.Motherboard);
        var controller = new FakeFanController("fan-1", HardwareNodeType.Motherboard) { CurrentPercent = 30 };
        var rpmSensor = new SensorReading("fan-1-rpm", "CPU fan", SensorType.Fan, 0, 0, 0, DateTimeOffset.UtcNow);

        card.UpdateReadings(controller, rpmSensor);

        Assert.Equal("0 RPM", card.RpmDisplayText);
    }

    [Fact]
    public void UpdateReadings_GpuFan_NonZeroRpm_ShowsOrdinaryRpmText()
    {
        var card = CreateCard("gpu-fan-1", HardwareNodeType.Gpu);
        var controller = new FakeFanController("gpu-fan-1", HardwareNodeType.Gpu) { CurrentPercent = 60 };
        var rpmSensor = new SensorReading("gpu-fan-1-rpm", "GPU fan", SensorType.Fan, 1800, 1800, 1800, DateTimeOffset.UtcNow);

        card.UpdateReadings(controller, rpmSensor);

        Assert.Equal("1800 RPM", card.RpmDisplayText);
    }

    /// <summary>Minimal local <see cref="IFanController"/> fake - this test project has no
    /// dependency on <c>Porchlight.Core.Tests</c>' own fake.</summary>
    private sealed class FakeFanController(string id, HardwareNodeType nodeType) : IFanController
    {
        public string Id { get; } = id;

        public string Name => "Fake fan";

        public HardwareNodeType NodeType { get; } = nodeType;

        public double? CurrentPercent { get; set; }

        public bool IsUnderSoftwareControl => false;

        public bool CanControl => true;

        public string? RpmSensorId => null;

        public double MinSoftwarePercent => 0;

        public double MaxSoftwarePercent => 100;

        public void SetPercent(double percent) => CurrentPercent = percent;

        public void RestoreDefault() => CurrentPercent = null;
    }
}
