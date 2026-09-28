#if DEBUG
namespace Porchlight.Core.Hardware.Demo;

/// <summary>
/// DEBUG-only fake <see cref="IHardwareService"/> for the "demo data" mode (see
/// <c>Monitoring.Demo.DemoDataMode</c> and CONTRIBUTING.md's "Screenshots" section) - publishes a
/// fixed, made-up sensor tree and fake controllable fans so a documentation screenshot of the
/// Hardware page never shows the real machine's sensors. Never opens the real hardware library and
/// never touches real hardware; <see cref="Start"/> simply raises one snapshot synchronously.
/// </summary>
/// <remarks>
/// Spec 10: the device set here is deliberately realistic - a Ryzen CPU (Tctl/Tdie, per-core
/// temperatures/clocks/loads with genuinely mixed values, package power, a Factor/multiplier
/// sensor), an NVIDIA GPU (core + hot spot temperature, fans), a Nuvoton motherboard (several
/// voltage rails, several fans including a couple of unconnected 0 RPM headers), and an NVMe drive
/// - so the new card layout, summary strip, and "Hide unused sensors" toggle all have something
/// real to show in a screenshot.
/// </remarks>
internal sealed class DemoHardwareService : IHardwareService
{
    private readonly DemoFanController _cpuFan = new("demo-cpu-fan", "CPU fan", 42, "demo-cpu-fan-rpm");
    private readonly DemoFanController _caseFan1 = new("demo-case-fan-1", "Case fan 1", 55, "demo-case-fan-1-rpm");
    private readonly DemoFanController _gpuFan = new("demo-gpu-fan", "GPU fan 1", 38, "demo-gpu-fan-rpm");

    public event EventHandler<HardwareSnapshot>? SnapshotUpdated;

    public HardwareSnapshot Latest { get; private set; } = HardwareSnapshot.Empty(HardwareStatus.NotElevated);

    public IReadOnlyList<IFanController> Controllers { get; }

    public DemoHardwareService()
    {
        Controllers = [_cpuFan, _caseFan1, _gpuFan];
        Latest = BuildSnapshot();
    }

    public void Start() => SnapshotUpdated?.Invoke(this, Latest);

    public void Stop()
    {
        // No real hardware to release; nothing to do.
    }

    public void ResetMinMax() => Latest = BuildSnapshot();

    public void RunOnOwnerThread(Action action, TimeSpan timeout, bool allowDirectFallback = true) => action();

    private static HardwareSnapshot BuildSnapshot()
    {
        var now = DateTimeOffset.UtcNow;

        SensorReading Reading(string id, string name, SensorType type, double? value, double? min, double? max) =>
            new(id, name, type, value, min, max, now);

        var cpuSensors = new List<SensorReading>
        {
            // AMD reports Tctl/Tdie rather than a generic "Package" temperature - the summary strip
            // and HardwareSummarySelector prefer this over "Package" on purpose (spec 10).
            Reading("demo-cpu-tctl", "Core (Tctl/Tdie)", SensorType.Temperature, 61.4, 38.2, 78.9),
            Reading("demo-cpu-package-power", "Package", SensorType.Power, 68.3, 21.5, 105.2),
            Reading("demo-cpu-total-load", "CPU Total", SensorType.Load, 23.5, 4.1, 61.0),
            Reading("demo-cpu-fan-rpm", "CPU fan", SensorType.Fan, 1180, 900, 1800),
            // Genuinely mixed per-core load, unlike the maintainer's screenshot where every core
            // read 100% at once (see the PR description for the verified explanation).
            Reading("demo-cpu-core1-temp", "CCD0 (Tdie)", SensorType.Temperature, 60.1, 37.0, 77.5),
            Reading("demo-cpu-core1-clock", "Core #1", SensorType.Clock, 4512, 2200, 4950),
            Reading("demo-cpu-core1-load", "Core #1", SensorType.Load, 87.0, 2.0, 100.0),
            // The maintainer's "Core #1  34" row with no unit was this sensor - LHM's per-core
            // multiplier, SensorType.Factor - which the old formatter mapped to an empty unit
            // string. SensorFormatter now renders it as "34.00x".
            Reading("demo-cpu-core1-multiplier", "Core #1", SensorType.Factor, 34.0, 22.0, 49.5),
            Reading("demo-cpu-core2-clock", "Core #2", SensorType.Clock, 4108, 2200, 4950),
            Reading("demo-cpu-core2-load", "Core #2", SensorType.Load, 12.0, 1.0, 96.0),
            Reading("demo-cpu-core2-multiplier", "Core #2", SensorType.Factor, 30.5, 22.0, 49.5),
            Reading("demo-cpu-core3-clock", "Core #3", SensorType.Clock, 3600, 2200, 4950),
            Reading("demo-cpu-core3-load", "Core #3", SensorType.Load, 6.5, 0.5, 88.0),
            Reading("demo-cpu-core3-multiplier", "Core #3", SensorType.Factor, 27.0, 22.0, 49.5),
            Reading("demo-cpu-core4-clock", "Core #4", SensorType.Clock, 3400, 2200, 4950),
            Reading("demo-cpu-core4-load", "Core #4", SensorType.Load, 3.0, 0.5, 74.0),
            Reading("demo-cpu-core4-multiplier", "Core #4", SensorType.Factor, 25.5, 22.0, 49.5),
        };

        var cpu = new HardwareNode("demo-cpu", "AMD Ryzen 7 5800X3D (Demo)", HardwareNodeType.Cpu, cpuSensors, []);

        var gpuSensors = new List<SensorReading>
        {
            Reading("demo-gpu-core-temp", "GPU Core", SensorType.Temperature, 54.0, 34.0, 79.0),
            Reading("demo-gpu-hotspot-temp", "GPU Hot Spot", SensorType.Temperature, 66.0, 40.0, 92.0),
            Reading("demo-gpu-core-load", "GPU Core", SensorType.Load, 18.0, 0.0, 99.0),
            Reading("demo-gpu-memory-load", "GPU Memory Controller", SensorType.Load, 9.0, 0.0, 62.0),
            Reading("demo-gpu-core-clock", "GPU Core", SensorType.Clock, 1830, 210, 2610),
            Reading("demo-gpu-power", "GPU Power", SensorType.Power, 58.4, 12.0, 220.0),
            Reading("demo-gpu-fan-rpm", "GPU fan 1", SensorType.Fan, 1120, 0, 2400),
            // A second GPU fan header that never spins up under normal load - stays hidden once
            // "Hide unused sensors" is on, since it has only ever reported 0.
            Reading("demo-gpu-fan2-rpm", "GPU fan 2", SensorType.Fan, 0, 0, 0),
        };

        var gpu = new HardwareNode("demo-gpu", "NVIDIA GeForce RTX 4070 SUPER (Demo)", HardwareNodeType.Gpu, gpuSensors, []);

        var motherboardSensors = new List<SensorReading>
        {
            Reading("demo-vcore", "CPU VCore", SensorType.Voltage, 1.284, 1.062, 1.412),
            Reading("demo-vsoc", "CPU SOC", SensorType.Voltage, 1.052, 0.912, 1.100),
            Reading("demo-v12", "+12V", SensorType.Voltage, 12.06, 11.84, 12.14),
            Reading("demo-v5", "+5V", SensorType.Voltage, 5.02, 4.94, 5.08),
            Reading("demo-v33", "+3.3V", SensorType.Voltage, 3.31, 3.26, 3.34),
            Reading("demo-dram", "DRAM", SensorType.Voltage, 1.352, 1.340, 1.360),
            Reading("demo-cpu-fan-rpm-mb", "CPU fan", SensorType.Fan, 1180, 900, 1800),
            Reading("demo-case-fan-1-rpm", "Case fan 1", SensorType.Fan, 1350, 1000, 1600),
            // Unconnected headers - the maintainer's "Fan #1/#4-#7 0 RPM (min 0, max 0)" noise.
            // These stay hidden once "Hide unused sensors" is on, since a fan that has never once
            // reported a real RPM never contributes anything useful to read.
            Reading("demo-fan-header-3", "Fan #3", SensorType.Fan, 0, 0, 0),
            Reading("demo-fan-header-4", "Fan #4", SensorType.Fan, 0, 0, 0),
            Reading("demo-fan-header-5", "Fan #5", SensorType.Fan, 0, 0, 0),
            // Same idea for an unpopulated voltage rail some boards expose but never wire up.
            Reading("demo-voltage-unused", "Voltage #7", SensorType.Voltage, 0, 0, 0),
        };

        var motherboard = new HardwareNode("demo-motherboard", "Nuvoton NCT6798D (Demo)", HardwareNodeType.Motherboard, motherboardSensors, []);

        var memory = new HardwareNode(
            "demo-memory",
            "Demo Memory",
            HardwareNodeType.Memory,
            [
                Reading("demo-memory-load", "Memory", SensorType.Load, 34.0, 20.0, 48.0),
                Reading("demo-memory-used", "Memory Used", SensorType.Data, 10.8, 6.1, 15.4),
                Reading("demo-memory-available", "Memory Available", SensorType.Data, 21.2, 16.6, 25.9),
            ],
            []);

        var storage = new HardwareNode(
            "demo-storage",
            "Demo NVMe SSD 1TB",
            HardwareNodeType.Storage,
            [
                Reading("demo-storage-temp", "Temperature", SensorType.Temperature, 41.0, 32.0, 58.0),
                Reading("demo-storage-used", "Used Space", SensorType.Data, 412.0, 380.0, 412.0),
                Reading("demo-storage-read", "Read Rate", SensorType.Throughput, 2_400_000, 0, 118_000_000),
                Reading("demo-storage-write", "Write Rate", SensorType.Throughput, 512_000, 0, 62_000_000),
            ],
            []);

        return new HardwareSnapshot(
            HardwareStatus.Ready,
            null,
            [cpu, gpu, motherboard, memory, storage],
            now);
    }
}
#endif
