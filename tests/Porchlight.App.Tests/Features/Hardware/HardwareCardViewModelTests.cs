using Porchlight.App.Features.Hardware;
using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.App.Tests.Features.Hardware;

/// <summary>
/// Spec 10: one Sensors-tab card per hardware device - sections by sensor type, the collapsed
/// header's live stats, in-place merging across ticks, and filter/"hide unused" visibility.
/// </summary>
public sealed class HardwareCardViewModelTests
{
    private static SensorReading Sensor(string id, string name, SensorType type, double? value) =>
        new(id, name, type, value, value, value, DateTimeOffset.UtcNow);

    private static HardwareNode Node(
        HardwareNodeType type, string name, IReadOnlyList<SensorReading> sensors, IReadOnlyList<HardwareNode>? children = null) =>
        new("node-1", name, type, sensors, children ?? []);

    /// <summary>A CPU node whose two key collapsed-header stats are "69.8 °C" and "22 %".</summary>
    private static HardwareNode CpuWithStats() =>
        Node(HardwareNodeType.Cpu, "Intel CPU",
        [
            Sensor("cpu-temp", "CPU Package", SensorType.Temperature, 69.8),
            Sensor("cpu-load", "CPU Total", SensorType.Load, 22),
        ]);

    private static HardwareCardViewModel CreateCard(HardwareNode node, UnusedSensorTracker? tracker = null) =>
        new(node, tracker ?? new UnusedSensorTracker());

    private static string VisibleRowIds(HardwareCardViewModel card) =>
        string.Join(",", card.Sections.SelectMany(s => s.Sensors).Where(r => r.IsVisible).Select(r => r.Id));

    [Fact]
    public void Constructor_StartsCollapsed_EvenForCpuAndGpu()
    {
        var card = CreateCard(CpuWithStats());

        Assert.False(card.IsExpanded);
        Assert.Equal("Intel CPU", card.Name);
        Assert.Equal("node-1", card.Id);
    }

    [Theory]
    [InlineData(HardwareNodeType.Cpu, "")]
    [InlineData(HardwareNodeType.Gpu, "")]
    [InlineData(HardwareNodeType.Motherboard, "")]
    [InlineData(HardwareNodeType.Memory, "")]
    [InlineData(HardwareNodeType.Storage, "")]
    [InlineData(HardwareNodeType.Network, "")]
    [InlineData(HardwareNodeType.Other, "")] // anything unrecognized falls back to a generic glyph
    public void Icon_DependsOnHardwareType(HardwareNodeType type, string expectedGlyph)
    {
        var card = CreateCard(Node(type, "Device", []));

        Assert.Equal(expectedGlyph, card.Icon);
    }

    [Fact]
    public void Sections_FlattenChildNodeSensors_AndAreGroupedByTypeInSectionOrder()
    {
        var node = Node(HardwareNodeType.Cpu, "Intel CPU",
            [
                Sensor("load", "CPU Total", SensorType.Load, 10),
                Sensor("fan", "CPU Fan", SensorType.Fan, 900),
                Sensor("temp-package", "CPU Package", SensorType.Temperature, 50),
            ],
            [Node(HardwareNodeType.Cpu, "Core 0", [Sensor("temp-core0", "Core #0", SensorType.Temperature, 48)])]);

        var card = CreateCard(node);

        Assert.Equal([SensorType.Temperature, SensorType.Fan, SensorType.Load], card.Sections.Select(s => s.Type));
        Assert.Equal(["temp-package", "temp-core0"], card.Sections[0].Sensors.Select(r => r.Id));
    }

    [Fact]
    public void CollapsedStatsText_JoinsTheTwoKeyStatsWithASeparator()
    {
        var card = CreateCard(CpuWithStats());

        Assert.Equal("69.8 °C · 22 %", card.CollapsedStatsText);
    }

    [Theory]
    [InlineData(false, true, true)] // collapsed with stats: shown
    [InlineData(true, true, false)] // expanded: the same values are visible in the rows below
    [InlineData(false, false, false)] // collapsed but nothing usable to show
    public void ShowCollapsedStats_RequiresCollapsedCardAndSomeStats(bool expanded, bool hasStats, bool expected)
    {
        var card = CreateCard(hasStats ? CpuWithStats() : Node(HardwareNodeType.Cpu, "Intel CPU", []));
        card.IsExpanded = expanded;

        Assert.Equal(expected, card.ShowCollapsedStats);
    }

    [Theory]
    [InlineData(false, "Intel CPU, 69.8 °C, 22 %")] // a screen reader hears the same name + values a sighted user reads
    [InlineData(true, "Intel CPU")]
    public void HeaderAutomationName_IncludesCollapsedStatsOnlyWhileTheyAreShown(bool expanded, string expected)
    {
        var card = CreateCard(CpuWithStats());
        card.IsExpanded = expanded;

        Assert.Equal(expected, card.HeaderAutomationName);
    }

    [Fact]
    public void ExpandingTheCard_NotifiesTheCollapsedStatsDependentProperties()
    {
        var card = CreateCard(CpuWithStats());
        var changed = new List<string?>();
        card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        card.IsExpanded = true;

        Assert.Contains(nameof(HardwareCardViewModel.ShowCollapsedStats), changed);
        Assert.Contains(nameof(HardwareCardViewModel.HeaderAutomationName), changed);
    }

    [Fact]
    public void UpdateFrom_KeepsTheSameRowInstance_AndRefreshesItsValues()
    {
        var tracker = new UnusedSensorTracker();
        var card = CreateCard(Node(HardwareNodeType.Cpu, "Intel CPU", [Sensor("t", "CPU Package", SensorType.Temperature, 40)]), tracker);
        var rowBefore = card.Sections[0].Sensors[0];

        card.UpdateFrom(
            Node(HardwareNodeType.Cpu, "Intel CPU (renamed)", [Sensor("t", "CPU Package", SensorType.Temperature, 55)]), tracker);

        Assert.Same(rowBefore, card.Sections[0].Sensors[0]);
        Assert.Equal("55 °C", rowBefore.ValueText);
        Assert.Equal("Intel CPU (renamed)", card.Name);
    }

    [Fact]
    public void UpdateFrom_RemovesSectionsAndRowsWhoseSensorsDisappeared()
    {
        var tracker = new UnusedSensorTracker();
        var card = CreateCard(
            Node(HardwareNodeType.Cpu, "Intel CPU",
            [
                Sensor("t1", "CPU Package", SensorType.Temperature, 40),
                Sensor("t2", "Core #0", SensorType.Temperature, 38),
                Sensor("fan", "CPU Fan", SensorType.Fan, 900),
            ]), tracker);

        card.UpdateFrom(Node(HardwareNodeType.Cpu, "Intel CPU", [Sensor("t1", "CPU Package", SensorType.Temperature, 41)]), tracker);

        var section = Assert.Single(card.Sections);
        Assert.Equal(SensorType.Temperature, section.Type);
        Assert.Equal(["t1"], section.Sensors.Select(r => r.Id));
    }

    [Fact]
    public void UpdateFrom_NewSectionOfAnEarlierType_IsInsertedAtItsRankedPosition()
    {
        var tracker = new UnusedSensorTracker();
        var card = CreateCard(Node(HardwareNodeType.Cpu, "Intel CPU", [Sensor("load", "CPU Total", SensorType.Load, 10)]), tracker);

        card.UpdateFrom(
            Node(HardwareNodeType.Cpu, "Intel CPU",
            [
                Sensor("load", "CPU Total", SensorType.Load, 10),
                Sensor("temp", "CPU Package", SensorType.Temperature, 40),
            ]), tracker);

        Assert.Equal([SensorType.Temperature, SensorType.Load], card.Sections.Select(s => s.Type));
    }

    [Fact]
    public void UpdateFrom_AppliesAFanDisplayNameOverrideToThatFansRpmRow()
    {
        var node = Node(HardwareNodeType.Motherboard, "Board",
        [
            Sensor("rpm-1", "Fan #1", SensorType.Fan, 900),
            Sensor("rpm-2", "Fan #2", SensorType.Fan, 800),
        ]);

        var card = new HardwareCardViewModel(node, new UnusedSensorTracker(), new Dictionary<string, string> { ["rpm-1"] = "Front intake" });

        Assert.Equal(["Front intake", "Fan #2"], card.Sections[0].Sensors.Select(r => r.Name));
    }

    [Theory]
    [InlineData("", true, "t1,f1")] // no filter: everything shows
    [InlineData("intel", true, "t1,f1")] // the card's own name matches: every sensor under it shows (case-insensitive)
    [InlineData("PACKAGE", true, "t1")] // only the matching sensor shows, and its card stays visible
    [InlineData("zzz", false, "")] // nothing matches anywhere: the whole card hides
    public void ApplyFilter_ShowsMatchingRowsAndHidesCardsWithNoMatch(string filter, bool expectedCardVisible, string expectedRowIds)
    {
        var card = CreateCard(Node(HardwareNodeType.Cpu, "Intel CPU",
        [
            Sensor("t1", "CPU Package", SensorType.Temperature, 50),
            Sensor("f1", "CPU Fan", SensorType.Fan, 900),
        ]));

        card.ApplyFilter(filter, hideUnused: false);

        Assert.Equal(expectedCardVisible, card.IsVisible);
        Assert.Equal(expectedRowIds, VisibleRowIds(card));
    }

    [Theory]
    [InlineData(true, "used")] // an unconnected header (never reported RPM) is hidden...
    [InlineData(false, "unused,used")] // ...but only while "Hide unused sensors" is on
    public void ApplyFilter_HideUnused_HidesSensorsThatNeverReportedARealValue(bool hideUnused, string expectedRowIds)
    {
        var card = CreateCard(Node(HardwareNodeType.Motherboard, "Board",
        [
            Sensor("unused", "Fan #1", SensorType.Fan, 0),
            Sensor("used", "Fan #2", SensorType.Fan, 1200),
        ]));

        card.ApplyFilter(string.Empty, hideUnused);

        Assert.Equal(expectedRowIds, VisibleRowIds(card));
    }
}
