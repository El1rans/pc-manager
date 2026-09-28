using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>Spec 10 addendum: collapsed-card stats - the 2 most useful live values shown next to a
/// device's name while its card is collapsed.</summary>
public sealed class HardwareCardStatsSelectorTests
{
    private static SensorReading Reading(string id, string name, SensorType type, double? value) =>
        new(id, name, type, value, value, value, DateTimeOffset.UtcNow);

    [Fact]
    public void Cpu_PicksTemperatureThenTotalLoad()
    {
        var cpu = new HardwareNode("cpu", "AMD Ryzen 7 5800X3D", HardwareNodeType.Cpu,
            [
                Reading("c1", "Core #1", SensorType.Temperature, 58),
                Reading("t1", "Core (Tctl/Tdie)", SensorType.Temperature, 69.8),
                Reading("l1", "Core #1", SensorType.Load, 12),
                Reading("l2", "CPU Total", SensorType.Load, 22),
            ], []);

        var stats = HardwareCardStatsSelector.Build(cpu);

        Assert.Equal(["69.8 °C", "22 %"], stats);
    }

    [Fact]
    public void Gpu_PicksCoreTemperatureThenCoreLoad()
    {
        var gpu = new HardwareNode("gpu", "GPU", HardwareNodeType.Gpu,
            [
                Reading("t1", "GPU Hot Spot", SensorType.Temperature, 66),
                Reading("t2", "GPU Core", SensorType.Temperature, 54),
                Reading("l1", "GPU Memory Controller", SensorType.Load, 9),
                Reading("l2", "GPU Core", SensorType.Load, 18),
            ], []);

        var stats = HardwareCardStatsSelector.Build(gpu);

        Assert.Equal(["54 °C", "18 %"], stats);
    }

    [Fact]
    public void Memory_PicksLoadThenUsedGb()
    {
        var memory = new HardwareNode("mem", "Memory", HardwareNodeType.Memory,
            [
                Reading("l1", "Memory", SensorType.Load, 34),
                Reading("d1", "Memory Available", SensorType.Data, 21.2),
                Reading("d2", "Memory Used", SensorType.Data, 10.8),
            ], []);

        var stats = HardwareCardStatsSelector.Build(memory);

        Assert.Equal(["34 %", "10.8 GB"], stats);
    }

    [Fact]
    public void Storage_PicksTemperatureThenUsedSpacePercent_NotTheDataCapacitySensor()
    {
        var storage = new HardwareNode("ssd", "NVMe SSD", HardwareNodeType.Storage,
            [
                Reading("t1", "Temperature", SensorType.Temperature, 41),
                Reading("d1", "Used Space", SensorType.Data, 412), // GB - not this one
                Reading("l1", "Used Space", SensorType.Load, 41.2), // % - this one
            ], []);

        var stats = HardwareCardStatsSelector.Build(storage);

        Assert.Equal(["41 °C", "41.2 %"], stats);
    }

    [Fact]
    public void Motherboard_PicksHottestTemperatureThenFastestFan_IgnoringUnconnectedHeaders()
    {
        var motherboard = new HardwareNode("mb", "Nuvoton NCT6798D", HardwareNodeType.Motherboard,
            [
                Reading("t1", "CPU Core", SensorType.Temperature, 45),
                Reading("t2", "VRM", SensorType.Temperature, 58),
                Reading("f1", "Fan #3", SensorType.Fan, 0), // unconnected header - never picked
                Reading("f2", "Case fan", SensorType.Fan, 1350),
                Reading("f3", "CPU fan", SensorType.Fan, 1180),
            ], []);

        var stats = HardwareCardStatsSelector.Build(motherboard);

        Assert.Equal(["58 °C", "1350 RPM"], stats);
    }

    [Fact]
    public void Motherboard_NeverPicksAnInvertedTemperatureAsHottest()
    {
        var motherboard = new HardwareNode("mb", "Motherboard", HardwareNodeType.Motherboard,
            [
                Reading("t1", "Core #1 Distance to TjMax", SensorType.Temperature, 55), // inverted, would look "hottest"
                Reading("t2", "VRM", SensorType.Temperature, 40),
            ], []);

        var stats = HardwareCardStatsSelector.Build(motherboard);

        Assert.Equal(["40 °C"], stats);
    }

    [Fact]
    public void Network_PicksDownloadThenUpload_ByName()
    {
        var network = new HardwareNode("net", "Realtek Gaming 2.5GbE", HardwareNodeType.Network,
            [
                Reading("u1", "Upload Speed", SensorType.Throughput, 64_000),
                Reading("d1", "Download Speed", SensorType.Throughput, 850_000),
            ], []);

        var stats = HardwareCardStatsSelector.Build(network);

        Assert.Equal(["830.1 KB/s", "62.5 KB/s"], stats);
    }

    [Fact]
    public void Network_UnnamedThroughputSensors_FallsBackToFirstTwoDistinctSensors()
    {
        var network = new HardwareNode("net", "NIC", HardwareNodeType.Network,
            [
                Reading("s1", "Ethernet", SensorType.Throughput, 10_000),
                Reading("s2", "Ethernet", SensorType.Throughput, 5_000),
            ], []);

        var stats = HardwareCardStatsSelector.Build(network);

        Assert.Equal(2, stats.Count);
    }

    [Fact]
    public void FallbackType_PicksFirstTemperatureThenFirstLoad()
    {
        var other = new HardwareNode("misc", "Some device", HardwareNodeType.Other,
            [
                Reading("t1", "Temp 1", SensorType.Temperature, 30),
                Reading("l1", "Load 1", SensorType.Load, 5),
            ], []);

        var stats = HardwareCardStatsSelector.Build(other);

        Assert.Equal(["30 °C", "5 %"], stats);
    }

    [Fact]
    public void FewerThanTwoSensorsAvailable_ShowsWhatExists()
    {
        var storage = new HardwareNode("ssd", "SSD", HardwareNodeType.Storage,
            [Reading("t1", "Temperature", SensorType.Temperature, 41)], []);

        var stats = HardwareCardStatsSelector.Build(storage);

        Assert.Equal(["41 °C"], stats);
    }

    [Fact]
    public void NoUsableSensors_ShowsNothing()
    {
        var storage = new HardwareNode("ssd", "SSD", HardwareNodeType.Storage, [], []);

        var stats = HardwareCardStatsSelector.Build(storage);

        Assert.Empty(stats);
    }

    [Fact]
    public void SensorWithNullValue_IsTreatedAsMissing()
    {
        var cpu = new HardwareNode("cpu", "CPU", HardwareNodeType.Cpu,
            [
                Reading("t1", "Core (Tctl/Tdie)", SensorType.Temperature, null),
                Reading("l1", "CPU Total", SensorType.Load, 22),
            ], []);

        var stats = HardwareCardStatsSelector.Build(cpu);

        Assert.Equal(["22 %"], stats);
    }

    [Fact]
    public void SensorsOnChildNodes_AreIncluded()
    {
        var core = new HardwareNode("core1", "Core #1", HardwareNodeType.Cpu,
            [Reading("l1", "CPU Total", SensorType.Load, 22)], []);
        var cpu = new HardwareNode("cpu", "CPU", HardwareNodeType.Cpu,
            [Reading("t1", "Core (Tctl/Tdie)", SensorType.Temperature, 69.8)], [core]);

        var stats = HardwareCardStatsSelector.Build(cpu);

        Assert.Equal(["69.8 °C", "22 %"], stats);
    }
}
