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

    [Theory]
    [InlineData(HardwareNodeType.Motherboard, true, false, false)] // an empty motherboard header is hidden when "hide unused" is on
    [InlineData(HardwareNodeType.Motherboard, false, false, true)] // ...but visible when it is off
    [InlineData(HardwareNodeType.Motherboard, true, true, true)] // once real, always visible (spec 10's "hide unused" rule, reused here)
    [InlineData(HardwareNodeType.Gpu, true, false, true)] // a GPU fan is never hidden, whatever the setting
    public void UpdateVisibility_DependsOnNodeTypeSettingAndRpmHistory(
        HardwareNodeType nodeType, bool hideUnusedSensors, bool everReportedRpm, bool expectedVisible)
    {
        var card = CreateCard("fan-1", nodeType);

        // The RPM *history* is what is passed in: a motherboard fan whose most recent tick idled
        // back to 0 still stays visible once it has ever reported RPM.
        card.UpdateVisibility(hideUnusedSensors, everReportedRpm);

        Assert.Equal(expectedVisible, card.IsVisible);
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

    [Theory]
    [InlineData(HardwareNodeType.Gpu, 30, 0, "Stopped (idle)")] // a GPU fan at 0 RPM is idle, not faulty
    [InlineData(HardwareNodeType.Motherboard, 30, 0, "0 RPM")] // a motherboard fan at 0 RPM is shown as-is
    [InlineData(HardwareNodeType.Gpu, 60, 1800, "1800 RPM")]
    public void UpdateReadings_RpmDisplayText_DependsOnNodeTypeAndRpm(
        HardwareNodeType nodeType, double percent, double rpm, string expected)
    {
        var card = CreateCard("fan-1", nodeType);
        var controller = new FakeFanController("fan-1", nodeType) { CurrentPercent = percent };
        var rpmSensor = new SensorReading("fan-1-rpm", "Fan", SensorType.Fan, rpm, rpm, rpm, DateTimeOffset.UtcNow);

        card.UpdateReadings(controller, rpmSensor);

        Assert.Equal(expected, card.RpmDisplayText);
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
