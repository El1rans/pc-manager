using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Alerts;
using Porchlight.Core.Hardware;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Tests.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Alerts;

public sealed class AlertInputProviderTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeDriveMonitor _drives = new();
    private readonly FakeHardwareService _hardware = new();
    private readonly FakeRestartDetector _restart = new();

    private AlertInputProvider CreateProvider(IHardwareService? hardware = null) =>
        new(_drives, hardware ?? _hardware, _restart, _time, NullLogger<AlertInputProvider>.Instance);

    private static DriveSnapshot Drive(string name) => new(name, null, "NTFS", 1000, 500, false);

    private static HardwareNode Node(
        HardwareNodeType type, SensorReading[]? sensors = null, HardwareNode[]? children = null) =>
        new(type.ToString(), type.ToString(), type, sensors ?? [], children ?? []);

    private SensorReading Temperature(string name, double? value) =>
        new(name, name, SensorType.Temperature, value, null, null, _time.GetUtcNow());

    private HardwareSnapshot Snapshot(HardwareStatus status, params HardwareNode[] nodes) =>
        new(status, null, nodes, _time.GetUtcNow());

    [Fact]
    public void GetInputs_KeepsOnlyFixedDrives()
    {
        var fixedRoot = Path.GetPathRoot(Environment.SystemDirectory)!;
        _drives.Drives = [Drive(fixedRoot), Drive("1:\\"), Drive(UnusedDriveRoot())];

        var inputs = CreateProvider().GetInputs();

        Assert.Equal([fixedRoot], inputs.Drives.Select(d => d.Name));
    }

    [Theory]
    [MemberData(nameof(DriveReadFailures))]
    public void GetInputs_DriveReadFailing_ReportsNoDrives(Exception failure)
    {
        _drives.Failure = failure;

        Assert.Empty(CreateProvider().GetInputs().Drives);
    }

    public static TheoryData<Exception> DriveReadFailures => new()
    {
        new IOException("io"),
        new UnauthorizedAccessException("denied"),
        new InvalidOperationException("invalid"),
    };

    [Theory]
    [InlineData(HardwareStatus.Ready)]
    [InlineData(HardwareStatus.NotElevated)]
    public void GetInputs_UsableSnapshot_ReadsCpuAndGpuTemperaturesIncludingNestedNodes(HardwareStatus status)
    {
        var cpu = Node(HardwareNodeType.Cpu, children: [Node(HardwareNodeType.Other, [Temperature("CPU Package", 61)])]);
        var gpu = Node(HardwareNodeType.Gpu, [Temperature("GPU Core", 47)]);
        _hardware.RaiseSnapshot(Snapshot(status, cpu, gpu));

        var inputs = CreateProvider().GetInputs();

        Assert.Equal(61, inputs.CpuTemperatureC);
        Assert.Equal(47, inputs.GpuTemperatureC);
    }

    [Fact]
    public void GetInputs_PartWithNoTemperatureSensor_ReportsNullForThatPart()
    {
        _hardware.RaiseSnapshot(Snapshot(
            HardwareStatus.Ready, Node(HardwareNodeType.Cpu, [Temperature("CPU Package", 61)]), Node(HardwareNodeType.Memory)));

        var inputs = CreateProvider().GetInputs();

        Assert.Equal(61, inputs.CpuTemperatureC);
        Assert.Null(inputs.GpuTemperatureC);
    }

    [Theory]
    [InlineData(HardwareStatus.DriverMissing)]
    [InlineData(HardwareStatus.Error)]
    public void GetInputs_SnapshotInAnUnusableState_IgnoresTemperatures(HardwareStatus status)
    {
        _hardware.RaiseSnapshot(Snapshot(status, Node(HardwareNodeType.Cpu, [Temperature("CPU Package", 90)])));

        var inputs = CreateProvider().GetInputs();

        Assert.Null(inputs.CpuTemperatureC);
        Assert.Null(inputs.GpuTemperatureC);
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void GetInputs_SnapshotOlderThan30Seconds_IsIgnored(int ageSeconds, bool trusted)
    {
        _hardware.RaiseSnapshot(Snapshot(HardwareStatus.Ready, Node(HardwareNodeType.Cpu, [Temperature("CPU Package", 90)])));
        _time.Advance(TimeSpan.FromSeconds(ageSeconds));

        var inputs = CreateProvider().GetInputs();

        Assert.Equal(trusted ? 90 : null, inputs.CpuTemperatureC);
    }

    [Fact]
    public void GetInputs_HardwareServiceThrowing_ReportsNoTemperatures()
    {
        var inputs = CreateProvider(new ThrowingHardwareService()).GetInputs();

        Assert.Null(inputs.CpuTemperatureC);
        Assert.Null(inputs.GpuTemperatureC);
    }

    [Fact]
    public void GetInputs_PassesThroughAPendingRestart()
    {
        _restart.Pending = true;

        Assert.True(CreateProvider().GetInputs().RestartPending);
    }

    [Fact]
    public void GetInputs_RestartCheckFailing_AssumesNoRestartPending()
    {
        _restart.Failure = new InvalidOperationException("registry");

        Assert.False(CreateProvider().GetInputs().RestartPending);
    }

    /// <summary>A drive-letter root that does not exist on this machine, so it is not a fixed drive.</summary>
    private static string UnusedDriveRoot()
    {
        var present = DriveInfo.GetDrives().Select(d => d.Name[0]).ToHashSet();
        return $"{Enumerable.Range('A', 26).Select(c => (char)c).First(c => !present.Contains(c))}:\\";
    }

    private sealed class FakeDriveMonitor : IDriveMonitor
    {
        public IReadOnlyList<DriveSnapshot> Drives { get; set; } = [];

        public Exception? Failure { get; set; }

        public IReadOnlyList<DriveSnapshot> GetDrives() => Failure is null ? Drives : throw Failure;
    }

    private sealed class FakeRestartDetector : IRestartDetector
    {
        public bool Pending { get; set; }

        public Exception? Failure { get; set; }

        public bool IsRestartPending() => Failure is null ? Pending : throw Failure;
    }

    private sealed class ThrowingHardwareService : IHardwareService
    {
        public event EventHandler<HardwareSnapshot>? SnapshotUpdated
        {
            add { }
            remove { }
        }

        public HardwareSnapshot Latest => throw new InvalidOperationException("hardware library failed");

        public IReadOnlyList<IFanController> Controllers => [];

        public void Start() { }

        public void Stop() { }

        public void ResetMinMax() { }

        public void RunOnOwnerThread(Action action, TimeSpan timeout, bool allowDirectFallback = true) => action();
    }
}
