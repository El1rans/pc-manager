using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>Spec 10: "summary-tile selection (Tctl/Tdie preferred on AMD, Package on Intel, GPU
/// core + hot spot)".</summary>
public sealed class HardwareSummarySelectorTests
{
    private static SensorReading Reading(string id, string name, SensorType type, double? value) =>
        new(id, name, type, value, value, value, DateTimeOffset.UtcNow);

    [Fact]
    public void SelectCpuTemperature_Amd_PrefersTctlTdieOverAnyOtherTemperature()
    {
        var cpu = new HardwareNode("cpu", "AMD Ryzen 7", HardwareNodeType.Cpu,
            [
                Reading("t1", "Core #1", SensorType.Temperature, 55),
                Reading("t2", "Core (Tctl/Tdie)", SensorType.Temperature, 62),
            ], []);

        var selected = HardwareSummarySelector.SelectCpuTemperature(cpu);

        Assert.Equal("t2", selected!.Id);
    }

    [Fact]
    public void SelectCpuTemperature_Intel_FallsBackToPackage()
    {
        var cpu = new HardwareNode("cpu", "Intel Core i7", HardwareNodeType.Cpu,
            [
                Reading("t1", "Core #1", SensorType.Temperature, 48),
                Reading("t2", "CPU Package", SensorType.Temperature, 51),
            ], []);

        var selected = HardwareSummarySelector.SelectCpuTemperature(cpu);

        Assert.Equal("t2", selected!.Id);
    }

    [Fact]
    public void SelectCpuTemperature_NeitherTctlNorPackage_FallsBackToFirstValidTemperature()
    {
        var cpu = new HardwareNode("cpu", "Some CPU", HardwareNodeType.Cpu,
            [Reading("t1", "Core #1", SensorType.Temperature, 48)], []);

        var selected = HardwareSummarySelector.SelectCpuTemperature(cpu);

        Assert.Equal("t1", selected!.Id);
    }

    [Fact]
    public void Build_AmdSensorsReadingZero_AreHiddenRatherThanShownAsNormal()
    {
        // What LibreHardwareMonitor reports for a Ryzen when Porchlight is not elevated.
        var cpu = new HardwareNode("cpu", "AMD Ryzen 7", HardwareNodeType.Cpu,
            [
                Reading("t1", "Core (Tctl/Tdie)", SensorType.Temperature, 0),
                Reading("p1", "Package", SensorType.Power, 0),
            ], []);

        var tiles = HardwareSummarySelector.Build(new HardwareSnapshot(HardwareStatus.Ready, null, [cpu], DateTimeOffset.UtcNow), 95);

        Assert.DoesNotContain(tiles, t => t.Title is "CPU temperature" or "CPU package power");
    }

    [Fact]
    public void SelectCpuTemperature_Amd_ZeroTctl_FallsBackToAReadableSensor()
    {
        var cpu = new HardwareNode("cpu", "AMD Ryzen 7", HardwareNodeType.Cpu,
            [
                Reading("t1", "Core (Tctl/Tdie)", SensorType.Temperature, 0),
                Reading("t2", "CCD1 (Tdie)", SensorType.Temperature, 58),
            ], []);

        Assert.Equal("t2", HardwareSummarySelector.SelectCpuTemperature(cpu)!.Id);
    }

    [Fact]
    public void SelectCpuTemperature_NeverPicksAnInvertedDistanceToTjMaxSensor()
    {
        var cpu = new HardwareNode("cpu", "Intel Core i7", HardwareNodeType.Cpu,
            [Reading("t1", "Core #1 Distance to TjMax", SensorType.Temperature, 20)], []);

        Assert.Null(HardwareSummarySelector.SelectCpuTemperature(cpu));
    }

    [Fact]
    public void SelectGpuTemperature_PrefersCoreOverOtherTemperatures()
    {
        var gpu = new HardwareNode("gpu", "GPU", HardwareNodeType.Gpu,
            [
                Reading("g1", "GPU Hot Spot", SensorType.Temperature, 70),
                Reading("g2", "GPU Core", SensorType.Temperature, 55),
            ], []);

        Assert.Equal("g2", HardwareSummarySelector.SelectGpuTemperature(gpu)!.Id);
    }

    [Fact]
    public void SelectGpuHotSpot_FindsTheHotSpotSensor()
    {
        var gpu = new HardwareNode("gpu", "GPU", HardwareNodeType.Gpu,
            [
                Reading("g1", "GPU Core", SensorType.Temperature, 55),
                Reading("g2", "GPU Hot Spot", SensorType.Temperature, 70),
            ], []);

        Assert.Equal("g2", HardwareSummarySelector.SelectGpuHotSpot(gpu)!.Id);
    }

    [Fact]
    public void SelectCpuPackagePower_PrefersPackageNamedSensor()
    {
        var cpu = new HardwareNode("cpu", "CPU", HardwareNodeType.Cpu,
            [
                Reading("p1", "Core #1", SensorType.Power, 5),
                Reading("p2", "Package", SensorType.Power, 65),
            ], []);

        Assert.Equal("p2", HardwareSummarySelector.SelectCpuPackagePower(cpu)!.Id);
    }

    [Fact]
    public void SelectHottestFan_PicksTheHighestRpm_IgnoringUnconnectedZeroRpmHeaders()
    {
        var nodes = new List<HardwareNode>
        {
            new("mb", "Motherboard", HardwareNodeType.Motherboard,
                [
                    Reading("f1", "Fan #1", SensorType.Fan, 0),
                    Reading("f2", "Case fan", SensorType.Fan, 1350),
                    Reading("f3", "CPU fan", SensorType.Fan, 1180),
                ], []),
        };

        Assert.Equal("f2", HardwareSummarySelector.SelectHottestFan(nodes)!.Id);
    }

    [Fact]
    public void SelectHottestFan_NoFans_ReturnsNull() =>
        Assert.Null(HardwareSummarySelector.SelectHottestFan([]));

    [Fact]
    public void Build_ProducesTilesInOrder_WithSeverityFromFailsafeTemperature()
    {
        var snapshot = new HardwareSnapshot(
            HardwareStatus.Ready,
            null,
            [
                new HardwareNode("cpu", "AMD Ryzen", HardwareNodeType.Cpu,
                    [
                        Reading("ct", "Core (Tctl/Tdie)", SensorType.Temperature, 92), // >= failsafe -> Critical
                        Reading("cp", "Package", SensorType.Power, 68),
                    ], []),
                new HardwareNode("gpu", "GPU", HardwareNodeType.Gpu,
                    [Reading("gt", "GPU Core", SensorType.Temperature, 50)], []),
                new HardwareNode("mb", "Motherboard", HardwareNodeType.Motherboard,
                    [Reading("f1", "Case fan", SensorType.Fan, 1200)], []),
            ],
            DateTimeOffset.UtcNow);

        var tiles = HardwareSummarySelector.Build(snapshot, failsafeTemperatureC: 90);

        Assert.Equal(4, tiles.Count);
        Assert.Equal("CPU temperature", tiles[0].Title);
        Assert.Equal(HardwareSummarySeverity.Critical, tiles[0].Severity);
        Assert.Equal("GPU temperature", tiles[1].Title);
        Assert.Equal(HardwareSummarySeverity.Normal, tiles[1].Severity);
        Assert.Equal("CPU package power", tiles[2].Title);
        Assert.Equal("Hottest fan", tiles[3].Title);
    }

    [Fact]
    public void Build_NoCpuOrGpuNode_ProducesNoTilesForThem()
    {
        var snapshot = new HardwareSnapshot(
            HardwareStatus.Ready,
            null,
            [new HardwareNode("storage", "SSD", HardwareNodeType.Storage,
                [Reading("t", "Temperature", SensorType.Temperature, 40)], [])],
            DateTimeOffset.UtcNow);

        var tiles = HardwareSummarySelector.Build(snapshot, failsafeTemperatureC: 90);

        Assert.Empty(tiles);
    }
}
