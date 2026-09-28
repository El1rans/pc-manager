#if DEBUG
namespace Porchlight.Core.Hardware.Demo;

/// <summary>
/// DEBUG-only fake <see cref="IHardwareService"/> for the "demo data" mode (see
/// <c>Monitoring.Demo.DemoDataMode</c> and CONTRIBUTING.md's "Screenshots" section) - publishes a
/// fixed, made-up sensor tree and fake controllable fans so a documentation screenshot of the
/// Hardware page never shows the real machine's sensors. Never opens the real hardware library and
/// never touches real hardware; <see cref="Start"/> simply raises one snapshot synchronously.
/// </summary>
internal sealed class DemoHardwareService : IHardwareService
{
    private readonly DemoFanController _cpuFan = new("demo-cpu-fan", "CPU fan", 42, "demo-cpu-fan-rpm");
    private readonly DemoFanController _caseFan = new("demo-case-fan-1", "Case fan 1", 55, "demo-case-fan-1-rpm");

    public event EventHandler<HardwareSnapshot>? SnapshotUpdated;

    public HardwareSnapshot Latest { get; private set; } = HardwareSnapshot.Empty(HardwareStatus.NotElevated);

    public IReadOnlyList<IFanController> Controllers { get; }

    public DemoHardwareService()
    {
        Controllers = [_cpuFan, _caseFan];
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

        SensorReading Reading(string id, string name, SensorType type, double value, double min, double max) =>
            new(id, name, type, value, min, max, now);

        var cpu = new HardwareNode(
            "demo-cpu",
            "Demo CPU",
            HardwareNodeType.Cpu,
            [
                Reading("demo-cpu-package", "CPU Package", SensorType.Temperature, 52, 38, 68),
                Reading("demo-cpu-load", "CPU Total", SensorType.Load, 18, 6, 61),
                Reading("demo-cpu-clock", "CPU Core #1", SensorType.Clock, 4200, 3600, 4800),
                Reading("demo-cpu-fan-rpm", "CPU fan", SensorType.Fan, 1180, 900, 1800),
            ],
            []);

        var gpu = new HardwareNode(
            "demo-gpu",
            "Demo GPU",
            HardwareNodeType.Gpu,
            [
                Reading("demo-gpu-core", "GPU Core", SensorType.Temperature, 47, 35, 72),
                Reading("demo-gpu-load", "GPU Core", SensorType.Load, 4, 0, 55),
                Reading("demo-gpu-fan-rpm", "GPU fan", SensorType.Fan, 900, 0, 1600),
            ],
            []);

        var motherboard = new HardwareNode(
            "demo-motherboard",
            "Demo Motherboard",
            HardwareNodeType.Motherboard,
            [
                Reading("demo-cpu-fan-rpm", "CPU fan", SensorType.Fan, 1180, 900, 1800),
                Reading("demo-case-fan-1-rpm", "Case fan 1", SensorType.Fan, 1350, 1000, 1600),
                Reading("demo-vcore", "CPU VCore", SensorType.Voltage, 1.28, 1.10, 1.35),
            ],
            []);

        var memory = new HardwareNode(
            "demo-memory",
            "Demo Memory",
            HardwareNodeType.Memory,
            [Reading("demo-memory-load", "Memory", SensorType.Load, 34, 20, 48)],
            []);

        var storage = new HardwareNode(
            "demo-storage",
            "Demo SSD",
            HardwareNodeType.Storage,
            [Reading("demo-storage-temp", "Temperature", SensorType.Temperature, 39, 32, 45)],
            []);

        return new HardwareSnapshot(
            HardwareStatus.Ready,
            null,
            [cpu, gpu, motherboard, memory, storage],
            now);
    }
}
#endif
