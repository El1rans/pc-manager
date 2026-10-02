using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Hardware;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Hardware;
using Porchlight.Core.Settings;
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

    private static FanCardViewModel CreateCard(FakeSettingsStore settingsStore) =>
        new("fan-1", "CPU fan", HardwareNodeType.Motherboard, settingsStore, NullLogger<FanCardViewModel>.Instance);

    [Fact]
    public void Constructor_LoadsSavedProfile_WithoutWritingItBackToSettings()
    {
        var settingsStore = new FakeSettingsStore();
        settingsStore.Current.Hardware.FanProfiles["fan-1"] = new FanProfileSettings
        {
            Mode = FanMode.Curve,
            FixedPercent = 55,
            SourceSensorId = "cpu-temp",
            CurvePoints = [new FanCurvePoint(35, 30), new FanCurvePoint(60, 70)],
        };

        var card = CreateCard(settingsStore);

        Assert.Equal(FanMode.Curve, card.Mode);
        Assert.Equal(55, card.FixedPercent);
        Assert.Equal("cpu-temp", card.SourceSensorId);
        Assert.Equal([new FanCurvePoint(35, 30), new FanCurvePoint(60, 70)], card.CurvePoints.Select(p => p.ToPoint()));
        Assert.Equal(0, settingsStore.UpdateCallCount); // merely opening the page must never rewrite settings
    }

    [Fact]
    public void Constructor_WithoutASavedProfile_SeedsADefaultCurveStartingAtTheMinimumFloor()
    {
        var settingsStore = new FakeSettingsStore();
        settingsStore.Current.Hardware.MinFanPercent = 40;

        var card = CreateCard(settingsStore);

        Assert.Equal(
            [new FanCurvePoint(40, 40), new FanCurvePoint(50, 60), new FanCurvePoint(70, 80), new FanCurvePoint(85, 100)],
            card.CurvePoints.Select(p => p.ToPoint()));
    }

    [Fact]
    public void Constructor_SavedFixedPercentOfZero_FallsBackToTheMinimumFloor()
    {
        var settingsStore = new FakeSettingsStore();
        settingsStore.Current.Hardware.MinFanPercent = 35;
        settingsStore.Current.Hardware.FanProfiles["fan-1"] = new FanProfileSettings { FixedPercent = 0 };

        var card = CreateCard(settingsStore);

        Assert.Equal(35, card.FixedPercent);
    }

    [Fact]
    public void ChangingModeFixedPercentAndSourceSensor_PersistsTheProfileImmediatelyKeyedByFanId()
    {
        var settingsStore = new FakeSettingsStore();
        var card = CreateCard(settingsStore);

        card.Mode = FanMode.Fixed;
        card.FixedPercent = 55;
        card.SourceSensorId = "cpu-temp";

        var saved = settingsStore.Current.Hardware.FanProfiles["fan-1"];
        Assert.Equal(FanMode.Fixed, saved.Mode);
        Assert.Equal(55, saved.FixedPercent);
        Assert.Equal("cpu-temp", saved.SourceSensorId);
    }

    [Fact]
    public void SaveProfile_PersistsCurvePointsSortedByTemperature()
    {
        var settingsStore = new FakeSettingsStore();
        var card = CreateCard(settingsStore);
        card.CurvePoints.Clear();

        card.CurvePoints.Add(new EditableCurvePoint(70, 80));
        card.CurvePoints.Add(new EditableCurvePoint(40, 30));

        Assert.Equal(
            [new FanCurvePoint(40, 30), new FanCurvePoint(70, 80)],
            settingsStore.Current.Hardware.FanProfiles["fan-1"].CurvePoints);
    }

    [Theory]
    [InlineData(FanMode.Curve, 10, true)] // a point below the 30% floor makes the curve invalid
    [InlineData(FanMode.Curve, 60, false)]
    [InlineData(FanMode.Fixed, 10, false)] // an invalid curve only matters while the curve is the active mode
    public void CommitCurve_SetsCurveError_OnlyForAnInvalidCurveInCurveMode(FanMode mode, double secondPointPercent, bool expectError)
    {
        var card = CreateCard(new FakeSettingsStore());
        card.Mode = mode;

        card.CurvePoints[1].Percent = secondPointPercent;
        card.CommitCurveCommand.Execute(null);

        Assert.Equal(expectError, card.CurveError is not null);
    }

    [Fact]
    public void CommitCurve_PersistsDraggedPointsOnlyOnceTheDragCompletes()
    {
        var settingsStore = new FakeSettingsStore();
        var card = CreateCard(settingsStore);

        card.CurvePoints[1].Percent = 65; // mid-drag: the point moves but nothing is saved yet
        Assert.Empty(settingsStore.Current.Hardware.FanProfiles);

        card.CommitCurveCommand.Execute(null);

        Assert.Equal(65, settingsStore.Current.Hardware.FanProfiles["fan-1"].CurvePoints[1].Percent);
    }

    [Fact]
    public void UpdateMinFanPercent_RevalidatesTheCurveWithoutPersistingIt()
    {
        var settingsStore = new FakeSettingsStore();
        var card = CreateCard(settingsStore);
        card.Mode = FanMode.Curve;
        card.FixedPercent = 60; // above the floors used below, so raising the floor does not touch it
        Assert.Null(card.CurveError);
        var updatesBefore = settingsStore.UpdateCallCount;

        card.UpdateMinFanPercent(40); // the default curve's first point (30%) is now below the floor
        var errorAfterRaisingFloor = card.CurveError;
        card.UpdateMinFanPercent(30);

        Assert.NotNull(errorAfterRaisingFloor);
        Assert.Null(card.CurveError);
        Assert.Equal(updatesBefore, settingsStore.UpdateCallCount);
    }

    [Theory]
    [InlineData(35, 50, 50)] // a fixed target below the new floor is raised to it
    [InlineData(70, 50, 70)] // one already above the floor is left alone
    public void UpdateMinFanPercent_RaisesFixedPercentOnlyWhenItFallsBelowTheFloor(double fixedPercent, int newMinimum, double expectedFixed)
    {
        var card = CreateCard(new FakeSettingsStore());
        card.FixedPercent = fixedPercent;

        card.UpdateMinFanPercent(newMinimum);

        Assert.Equal(newMinimum, card.MinFanPercent);
        Assert.Equal(expectedFixed, card.FixedPercent);
    }

    [Fact]
    public void AddCurvePoint_AppendsAPointAboveTheHottestOne_AndPersistsIt()
    {
        var settingsStore = new FakeSettingsStore();
        var card = CreateCard(settingsStore);

        card.AddCurvePointCommand.Execute(null);

        var added = card.CurvePoints[^1];
        Assert.Equal(5, card.CurvePoints.Count);
        Assert.Equal((95, 100), (added.TemperatureC, added.Percent)); // +10 C, +10% capped at 100%
        Assert.Equal(5, settingsStore.Current.Hardware.FanProfiles["fan-1"].CurvePoints.Count);
    }

    [Fact]
    public void AddCurvePoint_NearTheTemperatureCeiling_CapsAt99AndThenCannotAddAnotherPoint()
    {
        var card = CreateCard(new FakeSettingsStore());
        card.CurvePoints[^1].TemperatureC = 92;
        var canExecuteChangedCount = 0;
        card.AddCurvePointCommand.CanExecuteChanged += (_, _) => canExecuteChangedCount++;

        card.AddCurvePointCommand.Execute(null);

        Assert.Equal(99, card.CurvePoints[^1].TemperatureC);
        Assert.False(card.CanAddCurvePoint);
        Assert.False(card.AddCurvePointCommand.CanExecute(null));
        Assert.True(canExecuteChangedCount > 0);
    }

    [Fact]
    public void AddCurvePoint_AtTheMaximumPointCount_IsDisabled()
    {
        var card = CreateCard(new FakeSettingsStore());
        card.CurvePoints.Clear();
        for (var i = 0; i < FanControlOptions.MaxCurvePoints; i++)
        {
            card.CurvePoints.Add(new EditableCurvePoint(40 + (i * 5), 30 + (i * 5)));
        }

        Assert.False(card.CanAddCurvePoint);
        Assert.False(card.AddCurvePointCommand.CanExecute(null));
    }

    [Fact]
    public void RemoveCurvePoint_DropsThatPointAndPersists()
    {
        var settingsStore = new FakeSettingsStore();
        var card = CreateCard(settingsStore);
        var toRemove = card.CurvePoints[1];

        card.RemoveCurvePointCommand.Execute(toRemove);

        Assert.DoesNotContain(toRemove, card.CurvePoints);
        Assert.Equal(3, settingsStore.Current.Hardware.FanProfiles["fan-1"].CurvePoints.Count);
    }

    [Fact]
    public void RemoveCurvePoint_AtTheMinimumPointCount_KeepsTheCurveValid()
    {
        var card = CreateCard(new FakeSettingsStore());
        while (card.CurvePoints.Count > FanControlOptions.MinCurvePoints)
        {
            card.RemoveCurvePointCommand.Execute(card.CurvePoints[0]);
        }

        card.RemoveCurvePointCommand.Execute(card.CurvePoints[0]);

        Assert.Equal(FanControlOptions.MinCurvePoints, card.CurvePoints.Count);
    }

    [Theory]
    [InlineData("Front intake", "Front intake")] // starts from the custom name already set...
    [InlineData("", "")] // ...but never pre-fills the hardware name the user would just be re-typing
    public void BeginRenaming_PrefillsTheEditBoxWithTheCustomNameOnly(string existingCustomName, string expectedEditText)
    {
        var settingsStore = new FakeSettingsStore();
        if (existingCustomName.Length > 0)
        {
            settingsStore.Current.Hardware.FanDisplayNames["fan-1"] = existingCustomName;
        }

        var card = CreateCard(settingsStore);

        card.BeginRenamingCommand.Execute(null);

        Assert.True(card.IsEditingName);
        Assert.Equal(expectedEditText, card.NameEditText);
    }

    [Fact]
    public void CommitRename_TrimsSurroundingWhitespace()
    {
        var settingsStore = new FakeSettingsStore();
        var card = CreateCard(settingsStore);

        card.BeginRenamingCommand.Execute(null);
        card.NameEditText = "  Front intake  ";
        card.CommitRenameCommand.Execute(null);

        Assert.Equal("Front intake", card.CustomName);
        Assert.Equal("Front intake", settingsStore.Current.Hardware.FanDisplayNames["fan-1"]);
    }

    [Fact]
    public void CommitRename_WithAnUnchangedName_EndsEditingWithoutWritingSettings()
    {
        var settingsStore = new FakeSettingsStore();
        var card = CreateCard(settingsStore);

        card.BeginRenamingCommand.Execute(null);
        card.CommitRenameCommand.Execute(null);

        Assert.False(card.IsEditingName);
        Assert.Equal(0, settingsStore.UpdateCallCount);
    }

    [Theory]
    [InlineData(HardwareNodeType.Gpu, "Stopped (idle)")]
    [InlineData(HardwareNodeType.Motherboard, "- RPM")] // no tachometer at all, unlike a reading of 0
    public void UpdateReadings_WithNoControllerOrRpmSensor_ClearsTheStaleReadings(HardwareNodeType nodeType, string expectedText)
    {
        var card = CreateCard("fan-1", nodeType);
        var rpmSensor = new SensorReading("fan-1-rpm", "Fan", SensorType.Fan, 1500, 1500, 1500, DateTimeOffset.UtcNow);
        card.UpdateReadings(new FakeFanController("fan-1", nodeType) { CurrentPercent = 50 }, rpmSensor);

        card.UpdateReadings(null, null);

        Assert.Null(card.CurrentPercent);
        Assert.Null(card.CurrentRpm);
        Assert.Equal(expectedText, card.RpmDisplayText);
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
