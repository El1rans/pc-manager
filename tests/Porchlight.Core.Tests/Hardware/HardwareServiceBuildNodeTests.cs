using LibreHardwareMonitor.Hardware;
using Porchlight.Core.Hardware;
using Xunit;
using LhmHardwareType = LibreHardwareMonitor.Hardware.HardwareType;
using LhmSensorType = LibreHardwareMonitor.Hardware.SensorType;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>B1 (recommended follow-up): proves <see cref="HardwareService.BuildNode"/> pairs a
/// control sensor with its RPM sensor by index, handles a control with no matching tachometer, and
/// leaves a bare tachometer (no control) as a plain read-only sensor row - using fakes for the LHM
/// interfaces since LHM's own types are plain, driver-free interfaces.</summary>
public sealed class HardwareServiceBuildNodeTests
{
    private static Dictionary<string, (double? Value, DateTimeOffset ObservedUtc)> NewObservationCache() => [];

    [Fact]
    public void BuildNode_ControlWithMatchingRpmSensor_PairsThemByIndex()
    {
        var hardware = new FakeLhmHardware("/motherboard/0", "Motherboard", LhmHardwareType.Motherboard);
        var rpm = new FakeLhmSensor("/motherboard/0/fan/0", "CPU fan", LhmSensorType.Fan, index: 0, hardware) { Value = 1200 };
        var control = new FakeLhmSensor("/motherboard/0/control/0", "CPU fan control", LhmSensorType.Control, index: 0, hardware) { Value = 45 };
        control.Control = new FakeLhmControl(control) { MinSoftwareValue = 20, MaxSoftwareValue = 100 };
        hardware.SensorList.Add(rpm);
        hardware.SensorList.Add(control);

        var controllers = new List<IFanController>();
        HardwareService.BuildNode(hardware, controllers, NewObservationCache());

        var fan = Assert.Single(controllers);
        Assert.Equal(control.Identifier.ToString(), fan.Id);
        Assert.Equal(rpm.Identifier.ToString(), fan.RpmSensorId);
        Assert.Equal(20d, fan.MinSoftwarePercent);
        Assert.Equal(100d, fan.MaxSoftwarePercent);
        Assert.Equal(HardwareNodeType.Motherboard, fan.NodeType);
    }

    [Fact]
    public void BuildNode_GpuControlSensor_ControllerReportsGpuNodeType()
    {
        // Fans polish addendum: the Fans tab must never hide a GPU fan regardless of its RPM
        // history - this requires knowing a controller's owning node type, which BuildNode derives
        // from the same hardware.HardwareType it already maps for the node itself.
        var hardware = new FakeLhmHardware("/gpu/0", "GPU", LhmHardwareType.GpuNvidia);
        var control = new FakeLhmSensor("/gpu/0/control/0", "GPU fan control", LhmSensorType.Control, index: 0, hardware) { Value = 60 };
        control.Control = new FakeLhmControl(control);
        hardware.SensorList.Add(control);

        var controllers = new List<IFanController>();
        HardwareService.BuildNode(hardware, controllers, NewObservationCache());

        var fan = Assert.Single(controllers);
        Assert.Equal(HardwareNodeType.Gpu, fan.NodeType);
    }

    [Fact]
    public void BuildNode_ControlWithNoMatchingRpmSensor_StillBuildsController_WithNullRpmId()
    {
        var hardware = new FakeLhmHardware("/gpu/0", "GPU", LhmHardwareType.GpuNvidia);
        var control = new FakeLhmSensor("/gpu/0/control/0", "GPU fan control", LhmSensorType.Control, index: 0, hardware) { Value = 60 };
        control.Control = new FakeLhmControl(control);
        hardware.SensorList.Add(control);

        var controllers = new List<IFanController>();
        HardwareService.BuildNode(hardware, controllers, NewObservationCache());

        var fan = Assert.Single(controllers);
        Assert.Null(fan.RpmSensorId);
    }

    [Fact]
    public void BuildNode_TachWithNoControl_IsAPlainReadOnlySensorRow_NotAController()
    {
        var hardware = new FakeLhmHardware("/motherboard/0", "Motherboard", LhmHardwareType.Motherboard);
        var rpm = new FakeLhmSensor("/motherboard/0/fan/1", "Pump", LhmSensorType.Fan, index: 1, hardware) { Value = 3000 };
        hardware.SensorList.Add(rpm);

        var controllers = new List<IFanController>();
        var node = HardwareService.BuildNode(hardware, controllers, NewObservationCache());

        Assert.Empty(controllers);
        var row = Assert.Single(node.Sensors);
        Assert.Equal("Pump", row.Name);
        Assert.Equal(3000d, row.Value);
    }

    [Fact]
    public void BuildNode_ControlWithNullControlChannel_IsNeverBuiltIntoAController()
    {
        // Defensive: a Control-typed sensor whose own .Control happens to be null (should not occur
        // in practice per LHM's source, but BuildNode must not throw if it ever does).
        var hardware = new FakeLhmHardware("/gpu/0", "GPU", LhmHardwareType.GpuAmd);
        var control = new FakeLhmSensor("/gpu/0/control/0", "GPU fan control", LhmSensorType.Control, index: 0, hardware) { Value = 60, Control = null };
        hardware.SensorList.Add(control);

        var controllers = new List<IFanController>();
        HardwareService.BuildNode(hardware, controllers, NewObservationCache());

        Assert.Empty(controllers);
    }

    // ---------------------------------------------------------------- N2: sensor freshness

    [Fact]
    public void StampObservationTime_ConstantValueAcrossManyTicks_StaysFresh()
    {
        var cache = NewObservationCache();
        var start = DateTimeOffset.UtcNow;

        var lastTimestamp = start;
        for (var tick = 0; tick < 30; tick++)
        {
            var now = start + TimeSpan.FromSeconds(tick);
            // Same value every tick (a steady idle temperature) - must still be treated as freshly
            // read each time, per N2, since it *was* read this tick (non-null after Update()).
            lastTimestamp = HardwareService.StampObservationTime("cpu/temp", 42.0, now, cache);
            Assert.Equal(now, lastTimestamp);
        }
    }

    [Fact]
    public void StampObservationTime_ValueGoesNull_KeepsAgingThePreviousTimestamp()
    {
        var cache = NewObservationCache();
        var t0 = DateTimeOffset.UtcNow;
        var firstStamp = HardwareService.StampObservationTime("cpu/temp", 42.0, t0, cache);
        Assert.Equal(t0, firstStamp);

        var t1 = t0 + TimeSpan.FromSeconds(10);
        var secondStamp = HardwareService.StampObservationTime("cpu/temp", null, t1, cache);

        Assert.Equal(t0, secondStamp); // still the last time it actually had a value
    }
}
