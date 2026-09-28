using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Lighting.Effects;
using Porchlight.Core.Tests.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class EffectEngineTests
{
    private static EffectDeviceInfo MakeDevice(int index, string name, int ledCount, int activeModeIndex = 1)
    {
        IReadOnlyList<EffectLedInfo> leds = [.. Enumerable.Range(0, ledCount).Select(i => new EffectLedInfo(i, $"LED {i}"))];
        IReadOnlyList<EffectZoneInfo> zones = [new EffectZoneInfo(0, "Zone", EffectZoneType.Linear, ledCount, 0, null, null, null)];
        IReadOnlyList<EffectModeInfo> modes =
        [
            new EffectModeInfo(0, "Direct", IsPerLed: true),
            new EffectModeInfo(1, "Static", IsPerLed: false),
        ];

        return new EffectDeviceInfo(index, name, ledCount, leds, zones, modes, activeModeIndex);
    }

    private static (EffectEngine Engine, FakeEffectDeviceClient Client, FakeTimeProvider TimeProvider) CreateEngine(
        FakeDeviceExclusionProvider? exclusions, params EffectDeviceInfo[] devices)
    {
        var client = new FakeEffectDeviceClient { Connected = false };
        client.Devices.AddRange(devices);
        var hardwareService = new FakeHardwareService();
        var updates = new FakePendingUpdateCountProvider();
        var keyPressSource = new FakeKeyPressSource();
        var timeProvider = new FakeTimeProvider();

        var engine = new EffectEngine(
            client, hardwareService, updates, exclusions ?? new FakeDeviceExclusionProvider(), keyPressSource,
            NullLogger<EffectEngine>.Instance, timeProvider);

        return (engine, client, timeProvider);
    }

    private static (EffectEngine Engine, FakeEffectDeviceClient Client, FakeTimeProvider TimeProvider) CreateEngine(
        params EffectDeviceInfo[] devices) => CreateEngine(exclusions: null, devices);

    /// <summary>
    /// Advances <paramref name="timeProvider"/> one frame period at a time, <paramref name="ticks"/>
    /// times, firing the engine's periodic timer once per step. <see cref="FakeTimeProvider.Advance"/>
    /// moves the clock to its final value before running any due callbacks, so a single large advance
    /// would have every periodic callback observe the same (final) elapsed time; stepping period by
    /// period instead reproduces how a real clock ticks between callbacks.
    /// </summary>
    private static void AdvanceTicks(FakeTimeProvider timeProvider, int ticks, int fps)
    {
        var period = TimeSpan.FromSeconds(1.0 / fps);
        for (var i = 0; i < ticks; i++)
        {
            timeProvider.Advance(period);
        }
    }

    [Fact]
    public void Start_SwitchesAssignedDeviceToDirectMode()
    {
        var device = MakeDevice(0, "Keyboard", 10, activeModeIndex: 1);
        var (engine, client, _) = CreateEngine(device);
        engine.SetAssignments([new EffectAssignment { DeviceKey = "Keyboard", EffectName = "Rainbow wave" }]);

        engine.Start(fps: 10);

        Assert.Contains((0, 0), client.SetModeCalls);
        engine.Stop();
    }

    [Fact]
    public void Stop_RestoresDevicesToTheirOriginalMode()
    {
        var device = MakeDevice(0, "Keyboard", 10, activeModeIndex: 1);
        var (engine, client, _) = CreateEngine(device);
        engine.SetAssignments([new EffectAssignment { DeviceKey = "Keyboard", EffectName = "Rainbow wave" }]);
        engine.Start(fps: 10);

        engine.Stop();

        Assert.Contains((0, 1), client.SetModeCalls); // restored to the original active mode index (1 = Static).
    }

    [Fact]
    public void Tick_NeverExceedsConfiguredFps()
    {
        var device = MakeDevice(0, "Keyboard", 10);
        var (engine, client, timeProvider) = CreateEngine(device);
        engine.SetAssignments([new EffectAssignment { DeviceKey = "Keyboard", EffectName = "Rainbow wave" }]);

        engine.Start(fps: 10);
        AdvanceTicks(timeProvider, ticks: 10, fps: 10);

        Assert.Equal(10, client.UpdateLedsCalls.Count);
        engine.Stop();
    }

    [Fact]
    public void Tick_IdenticalConsecutiveFrames_DoesNotResendUnchangedColors()
    {
        // No CPU temperature reading => CpuTemperatureEffect always renders the same fallback
        // color, so only the first tick should actually call UpdateLeds.
        var device = MakeDevice(0, "Keyboard", 10);
        var (engine, client, timeProvider) = CreateEngine(device);
        engine.SetAssignments([new EffectAssignment { DeviceKey = "Keyboard", EffectName = "CPU temperature" }]);

        engine.Start(fps: 10);
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.Single(client.UpdateLedsCalls);
        engine.Stop();
    }

    [Fact]
    public void Tick_OneDeviceThrows_OtherDeviceKeepsRunning()
    {
        var failing = MakeDevice(0, "Broken strip", 10);
        var healthy = MakeDevice(1, "Keyboard", 10);
        var (engine, client, timeProvider) = CreateEngine(failing, healthy);
        client.FailingDeviceIndex = 0;
        engine.SetAssignments(
        [
            new EffectAssignment { DeviceKey = "Broken strip", EffectName = "Rainbow wave" },
            new EffectAssignment { DeviceKey = "Keyboard", EffectName = "Rainbow wave" },
        ]);

        engine.Start(fps: 10);
        AdvanceTicks(timeProvider, ticks: 10, fps: 10);

        Assert.Contains("Broken strip", engine.PausedDeviceNames);
        Assert.Contains("Keyboard", engine.RunningDeviceNames);
        Assert.DoesNotContain("Broken strip", engine.RunningDeviceNames);
        Assert.True(client.UpdateLedsCalls.Count(c => c.DeviceIndex == 1) > 1);
        engine.Stop();
    }

    [Fact]
    public void Start_ExcludedDevice_IsNeverRendered()
    {
        var device = MakeDevice(0, "Keyboard", 10);
        var exclusions = new FakeDeviceExclusionProvider();
        exclusions.ExcludedDeviceNames.Add("Keyboard");
        var (engine, client, timeProvider) = CreateEngine(exclusions, device);
        engine.SetAssignments([new EffectAssignment { DeviceKey = "Keyboard", EffectName = "Rainbow wave" }]);

        engine.Start(fps: 10);
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(engine.RunningDeviceNames);
        Assert.Empty(client.UpdateLedsCalls);
        engine.Stop();
    }

    [Fact]
    public void Start_UnassignedDevice_IsNeverRendered()
    {
        var device = MakeDevice(0, "Keyboard", 10);
        var (engine, client, timeProvider) = CreateEngine(device);
        // No assignments set at all.

        engine.Start(fps: 10);
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(client.UpdateLedsCalls);
        engine.Stop();
    }
}
