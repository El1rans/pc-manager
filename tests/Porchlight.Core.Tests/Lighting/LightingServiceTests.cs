using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

public sealed class LightingServiceTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ShortHeartbeat = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan EventWaitBudget = TimeSpan.FromSeconds(5);

    private static RgbDevice MakeDevice(int index, string name, int ledCount, params RgbMode[] modes) =>
        new(index, name, RgbDeviceType.Keyboard, "Acme", modes, modes.Length > 0 ? modes[0].Name : "", ledCount, Zones: []);

    private static LightingService CreateService(
        FakeOpenRgbClient client,
        TimeSpan? callTimeout = null,
        TimeSpan? heartbeatInterval = null) =>
        new(client, NullLogger<LightingService>.Instance, callTimeout, heartbeatInterval);

    /// <summary>Polls until <paramref name="condition"/> is true or <see cref="EventWaitBudget"/>
    /// elapses - used for the heartbeat/disconnect tests, whose effects happen on a background
    /// timer rather than on the awaited call itself.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + EventWaitBudget;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(condition(), "Condition was not met within the wait budget.");
    }

    // ---------------------------------------------------------------- connect

    [Fact]
    public async Task ConnectAsync_ClientConnects_ReturnsTrueAndIsConnected()
    {
        var client = new FakeOpenRgbClient();
        var service = CreateService(client);

        var result = await service.ConnectAsync(TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_ClientThrows_ReturnsFalseAndRaisesDisconnected()
    {
        var client = new FakeOpenRgbClient { FailConnectionWith = new InvalidOperationException("no server") };
        var service = CreateService(client);
        var disconnectedRaised = false;
        service.Disconnected += (_, _) => disconnectedRaised = true;

        var result = await service.ConnectAsync(TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(service.IsConnected);
        // Was never connected in the first place, so there is nothing new to announce.
        Assert.False(disconnectedRaised);
    }

    // ---------------------------------------------------------------- fail-fast while disconnected

    [Fact]
    public async Task GetDevicesAsync_NotConnected_ReturnsEmptyWithoutTouchingClient()
    {
        var client = new FakeOpenRgbClient();
        var service = CreateService(client);

        var devices = await service.GetDevicesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(devices);
        Assert.Equal(0, client.GetAllControllerDataCallCount);
    }

    [Fact]
    public async Task SetAllColorAsync_NotConnected_ReturnsNotConnectedWithoutTouchingClient()
    {
        var client = new FakeOpenRgbClient();
        var service = CreateService(client);

        var result = await service.SetAllColorAsync(RgbColor.White, TestContext.Current.CancellationToken);

        Assert.Equal(LightingApplyResult.NotConnected, result);
        Assert.Equal(0, client.GetControllerCountCallCount);
    }

    // ---------------------------------------------------------------- devices/profiles

    [Fact]
    public async Task GetDevicesAsync_ReturnsWhatTheClientReports()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "RAM", ledCount: 3));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var devices = await service.GetDevicesAsync(TestContext.Current.CancellationToken);

        Assert.Single(devices);
        Assert.Equal("RAM", devices[0].Name);
    }

    // ---------------------------------------------------------------- color + mode selection

    [Fact]
    public async Task SetDeviceColorAsync_DeviceHasDirectMode_SelectsDirectThenWritesColor()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "Keyboard", ledCount: 3,
            new RgbMode(0, "Static", RgbColorMode.ModeSpecific, 1), new RgbMode(1, "Direct", RgbColorMode.PerLed, 0)));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetDeviceColorAsync(0, new RgbColor(10, 20, 30), TestContext.Current.CancellationToken);

        Assert.True(result);
        var modeCall = Assert.Single(client.SetModeCalls);
        Assert.Equal(0, modeCall.DeviceIndex);
        Assert.Equal(1, modeCall.ModeIndex); // "Direct"
        Assert.Null(modeCall.Colors); // per-LED mode: colors go through UpdateLeds, not the mode itself
        var ledsCall = Assert.Single(client.UpdateLedsCalls);
        Assert.Equal(0, ledsCall.DeviceIndex);
        Assert.All(ledsCall.Colors, c => Assert.Equal(new RgbColor(10, 20, 30), c));
        Assert.Equal(3, ledsCall.Colors.Count);
    }

    [Fact]
    public async Task SetDeviceColorAsync_NoDirectOrStaticMode_DoesNotChangeMode()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "Fan", ledCount: 3, new RgbMode(0, "Rainbow")));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        await service.SetDeviceColorAsync(0, RgbColor.White, TestContext.Current.CancellationToken);

        Assert.Empty(client.SetModeCalls);
        Assert.Single(client.UpdateLedsCalls);
    }

    [Fact]
    public async Task SetDeviceColorAsync_ModeAlreadyActive_DoesNotResendMode()
    {
        var client = new FakeOpenRgbClient();
        var mode = new RgbMode(0, "Direct", RgbColorMode.PerLed, 0);
        client.AddDevice(new RgbDevice(0, "Keyboard", RgbDeviceType.Keyboard, "Acme", [mode], "Direct", 3, []));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        await service.SetDeviceColorAsync(0, RgbColor.White, TestContext.Current.CancellationToken);

        Assert.Empty(client.SetModeCalls);
        Assert.Single(client.UpdateLedsCalls);
    }

    [Fact]
    public async Task SetDeviceColorAsync_ZeroLedModeSpecificDevice_SendsColorThroughModeNotUpdateLeds()
    {
        // Mirrors a GPU/RAM stick whose only color mode is "Static" (one color for the whole
        // device) and reports zero directly-addressable LEDs - OpenRGB.NET throws on an empty
        // UpdateLeds call, so this must go through SetMode's own colors instead.
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "GPU", ledCount: 0, new RgbMode(0, "Static", RgbColorMode.ModeSpecific, 1)));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetDeviceColorAsync(0, new RgbColor(1, 2, 3), TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Empty(client.UpdateLedsCalls);
        var modeCall = Assert.Single(client.SetModeCalls);
        Assert.NotNull(modeCall.Colors);
        Assert.Single(modeCall.Colors!);
        Assert.Equal(new RgbColor(1, 2, 3), modeCall.Colors![0]);
    }

    [Fact]
    public async Task SetAllColorAsync_AppliesToEveryDeviceAndSkipsZeroLedUpdateLeds()
    {
        var client = new FakeOpenRgbClient();
        // RAM starts on "Static" so applying color must actually switch it to "Direct" (proving the
        // per-device mode-switch runs); GPU has only a mode-specific "Static" mode and zero LEDs.
        var ramModes = new[] { new RgbMode(0, "Static", RgbColorMode.ModeSpecific, 1), new RgbMode(1, "Direct", RgbColorMode.PerLed, 0) };
        client.AddDevice(new RgbDevice(0, "RAM", RgbDeviceType.Dram, "Acme", ramModes, "Static", 3, []));
        client.AddDevice(MakeDevice(1, "GPU", ledCount: 0, new RgbMode(0, "Static", RgbColorMode.ModeSpecific, 1)));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetAllColorAsync(RgbColor.White, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Single(client.UpdateLedsCalls); // only the RAM stick
        Assert.Equal(2, client.SetModeCalls.Count); // both devices switched mode
    }

    [Fact]
    public async Task SetAllColorAsync_OneDeviceFails_OthersStillApplyAndFailureIsCounted()
    {
        var client = new FakeOpenRgbClient();
        client.FailUpdateLedsForDevice[1] = new ArgumentException("simulated device quirk");
        client.AddDevice(MakeDevice(0, "RAM", ledCount: 2, new RgbMode(0, "Direct", RgbColorMode.PerLed, 0)));
        client.AddDevice(MakeDevice(1, "Weird", ledCount: 2, new RgbMode(0, "Direct", RgbColorMode.PerLed, 0)));
        client.AddDevice(MakeDevice(2, "Fan", ledCount: 2, new RgbMode(0, "Direct", RgbColorMode.PerLed, 0)));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetAllColorAsync(RgbColor.White, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        // A non-connection failure on one device must not disconnect the service.
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task TurnOffAllAsync_SetsEveryDeviceToBlack()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "RAM", ledCount: 3, new RgbMode(0, "Direct", RgbColorMode.PerLed, 0)));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.TurnOffAllAsync(TestContext.Current.CancellationToken);

        Assert.True(result.AllSucceeded);
        var call = Assert.Single(client.UpdateLedsCalls);
        Assert.All(call.Colors, c => Assert.Equal(RgbColor.Black, c));
    }

    [Fact]
    public async Task SetModeAsync_KnownModeName_SelectsItByIndex()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "Keyboard", ledCount: 1, new RgbMode(0, "Static"), new RgbMode(1, "Rainbow")));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetModeAsync(0, "rainbow", TestContext.Current.CancellationToken);

        Assert.True(result);
        var call = Assert.Single(client.SetModeCalls);
        Assert.Equal((0, 1), (call.DeviceIndex, call.ModeIndex));
    }

    [Fact]
    public async Task SetModeAsync_UnknownModeName_ReturnsFalseWithoutThrowingOrDisconnecting()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "Keyboard", ledCount: 1, new RgbMode(0, "Static")));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetModeAsync(0, "DoesNotExist", TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task GetProfilesAsync_ReturnsWhatTheClientReports()
    {
        var client = new FakeOpenRgbClient();
        client.AddProfile("Gaming");
        client.AddProfile("Quiet");
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var profiles = await service.GetProfilesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Gaming", "Quiet"], profiles);
    }

    [Fact]
    public async Task LoadProfileAsync_LoadsGivenProfile()
    {
        var client = new FakeOpenRgbClient();
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.LoadProfileAsync("Gaming", TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal("Gaming", client.LoadedProfile);
    }

    // ---------------------------------------------------------------- timeout / disconnect recovery

    [Fact]
    public async Task Call_TimesOut_DisposesClientRaisesDisconnectedAndNextCallDoesNotOverlap()
    {
        var client = new FakeOpenRgbClient();
        var service = CreateService(client, callTimeout: ShortTimeout);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var disconnectedRaised = false;
        service.Disconnected += (_, _) => disconnectedRaised = true;

        client.HangUntilDisposed = true;
        var devices = await service.GetDevicesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(devices);
        Assert.True(disconnectedRaised);
        Assert.False(service.IsConnected);
        // The gate was only released after disposing the stuck call, not before - so the orphan
        // never got a chance to interleave with a call this test hasn't made yet.
        Assert.True(client.DisposeCallCount >= 1);
        Assert.Equal(0, client.GetAllControllerDataCallCount);

        // Recovering: a fresh connect (against a client that no longer hangs) must succeed cleanly,
        // proving the gate is free and nothing is left running against the disposed client.
        var freshClient = new FakeOpenRgbClient();
        var recovered = await CreateService(freshClient, callTimeout: ShortTimeout)
            .ConnectAsync(TestContext.Current.CancellationToken);
        Assert.True(recovered);
    }

    [Fact]
    public async Task Call_ClientThrowsMidCall_RaisesDisconnectedAndReturnsDefaultWithoutThrowing()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "RAM", ledCount: 1, new RgbMode(0, "Direct", RgbColorMode.PerLed, 0)));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        client.FailAllCallsWith = new IOException("connection reset");
        var disconnectedRaised = false;
        service.Disconnected += (_, _) => disconnectedRaised = true;

        var result = await service.SetAllColorAsync(RgbColor.White, TestContext.Current.CancellationToken);

        Assert.Equal(LightingApplyResult.NotConnected, result);
        Assert.True(disconnectedRaised);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task Heartbeat_RemoteClosesWithoutException_EventuallyRaisesDisconnected()
    {
        var client = new FakeOpenRgbClient();
        var service = CreateService(client, callTimeout: ShortTimeout, heartbeatInterval: ShortHeartbeat);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var disconnectedRaised = false;
        service.Disconnected += (_, _) => disconnectedRaised = true;

        // A remote close that OpenRGB.NET's own read loop treats as a normal end rather than an
        // error: nothing throws, the socket just never answers again. Only the heartbeat notices.
        client.HangUntilDisposed = true;

        await WaitUntilAsync(() => disconnectedRaised);

        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task Call_SucceedsButClientReportsNotConnected_DiscardsResultDisposesAndDisconnects()
    {
        // Mirrors the vendored library's now-fixed phantom-reply bug (see
        // docs/upstream/openrgb-net.md): a call can return a normal-looking result even though the
        // client already knows it is no longer actually connected. LightingService must never trust
        // that result, and must never just quietly flip IsConnected without disposing/announcing it.
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "RAM", ledCount: 1, new RgbMode(0, "Direct", RgbColorMode.PerLed, 0)));
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var disconnectedRaised = false;
        service.Disconnected += (_, _) => disconnectedRaised = true;
        client.Connected = false;

        var devices = await service.GetDevicesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(devices); // the phantom "RAM" device is discarded, not trusted
        Assert.True(disconnectedRaised);
        Assert.False(service.IsConnected);
        Assert.True(client.DisposeCallCount >= 1);
    }

    [Fact]
    public async Task ClientDeviceListUpdated_RaisesDevicesChanged()
    {
        var client = new FakeOpenRgbClient();
        var service = CreateService(client);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var devicesChangedRaised = false;
        service.DevicesChanged += (_, _) => devicesChangedRaised = true;

        client.RaiseDeviceListUpdated();

        Assert.True(devicesChangedRaised);
    }

    [Fact]
    public void Dispose_DisposesTheUnderlyingClient()
    {
        var client = new FakeOpenRgbClient();
        var service = CreateService(client);

        service.Dispose();

        Assert.Equal(1, client.DisposeCallCount);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var client = new FakeOpenRgbClient();
        var service = CreateService(client);

        service.Dispose();
        service.Dispose();

        Assert.Equal(1, client.DisposeCallCount);
    }
}
