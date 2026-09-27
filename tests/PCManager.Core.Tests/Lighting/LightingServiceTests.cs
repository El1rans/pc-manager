using Microsoft.Extensions.Logging.Abstractions;
using PCManager.Core.Lighting;
using Xunit;

namespace PCManager.Core.Tests.Lighting;

public sealed class LightingServiceTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);

    private static RgbDevice MakeDevice(int index, string name, params RgbMode[] modes) =>
        new(index, name, RgbDeviceType.Keyboard, "Acme", modes, modes.Length > 0 ? modes[0].Name : "", LedCount: 3, Zones: []);

    [Fact]
    public async Task ConnectAsync_ClientConnects_ReturnsTrueAndIsConnected()
    {
        var client = new FakeOpenRgbClient();
        var service = new LightingService(client, NullLogger<LightingService>.Instance);

        var result = await service.ConnectAsync(TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_ClientThrows_ReturnsFalseAndRaisesDisconnected()
    {
        var client = new FakeOpenRgbClient { FailConnectionWith = new InvalidOperationException("no server") };
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        var disconnectedRaised = false;
        service.Disconnected += (_, _) => disconnectedRaised = true;

        var result = await service.ConnectAsync(TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(service.IsConnected);
        Assert.True(disconnectedRaised);
    }

    [Fact]
    public async Task GetDevicesAsync_ReturnsWhatTheClientReports()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "RAM"));
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var devices = await service.GetDevicesAsync(TestContext.Current.CancellationToken);

        Assert.Single(devices);
        Assert.Equal("RAM", devices[0].Name);
    }

    [Fact]
    public async Task SetDeviceColorAsync_DeviceHasDirectMode_SelectsDirectThenWritesColor()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "Keyboard", new RgbMode(0, "Static"), new RgbMode(1, "Direct")));
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetDeviceColorAsync(0, new RgbColor(10, 20, 30), TestContext.Current.CancellationToken);

        Assert.True(result);
        var modeCall = Assert.Single(client.SetModeCalls);
        Assert.Equal((0, 1), modeCall); // index 1 == "Direct"
        var ledsCall = Assert.Single(client.UpdateLedsCalls);
        Assert.Equal(0, ledsCall.DeviceIndex);
        Assert.All(ledsCall.Colors, c => Assert.Equal(new RgbColor(10, 20, 30), c));
        Assert.Equal(3, ledsCall.Colors.Count);
    }

    [Fact]
    public async Task SetDeviceColorAsync_NoDirectOrStaticMode_DoesNotChangeMode()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "Fan", new RgbMode(0, "Rainbow")));
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        await service.SetDeviceColorAsync(0, RgbColor.White, TestContext.Current.CancellationToken);

        Assert.Empty(client.SetModeCalls);
        Assert.Single(client.UpdateLedsCalls);
    }

    [Fact]
    public async Task SetAllColorAsync_AppliesToEveryDevice()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "RAM", new RgbMode(0, "Direct")));
        client.AddDevice(MakeDevice(1, "GPU", new RgbMode(0, "Direct")));
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetAllColorAsync(RgbColor.White, TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(2, client.UpdateLedsCalls.Count);
    }

    [Fact]
    public async Task TurnOffAllAsync_SetsEveryDeviceToBlack()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "RAM", new RgbMode(0, "Direct")));
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        await service.TurnOffAllAsync(TestContext.Current.CancellationToken);

        var call = Assert.Single(client.UpdateLedsCalls);
        Assert.All(call.Colors, c => Assert.Equal(RgbColor.Black, c));
    }

    [Fact]
    public async Task SetModeAsync_KnownModeName_SelectsItByIndex()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "Keyboard", new RgbMode(0, "Static"), new RgbMode(1, "Rainbow")));
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetModeAsync(0, "rainbow", TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal((0, 1), Assert.Single(client.SetModeCalls));
    }

    [Fact]
    public async Task SetModeAsync_UnknownModeName_ReturnsFalseWithoutThrowing()
    {
        var client = new FakeOpenRgbClient();
        client.AddDevice(MakeDevice(0, "Keyboard", new RgbMode(0, "Static")));
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.SetModeAsync(0, "DoesNotExist", TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task GetProfilesAsync_ReturnsWhatTheClientReports()
    {
        var client = new FakeOpenRgbClient();
        client.AddProfile("Gaming");
        client.AddProfile("Quiet");
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var profiles = await service.GetProfilesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Gaming", "Quiet"], profiles);
    }

    [Fact]
    public async Task LoadProfileAsync_LoadsGivenProfile()
    {
        var client = new FakeOpenRgbClient();
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await service.LoadProfileAsync("Gaming", TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal("Gaming", client.LoadedProfile);
    }

    [Fact]
    public async Task Call_TimesOut_RaisesDisconnectedAndReturnsDefaultWithoutThrowing()
    {
        var client = new FakeOpenRgbClient();
        var service = new LightingService(client, NullLogger<LightingService>.Instance, ShortTimeout);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        var disconnectedRaised = false;
        service.Disconnected += (_, _) => disconnectedRaised = true;

        client.HangCalls = true;
        try
        {
            var devices = await service.GetDevicesAsync(TestContext.Current.CancellationToken);

            Assert.Empty(devices);
            Assert.True(disconnectedRaised);
            Assert.False(service.IsConnected);
        }
        finally
        {
            // Release the fake's spin-wait so its background Task.Run eventually completes,
            // regardless of whether the assertions above passed.
            client.Unblock();
        }
    }

    [Fact]
    public async Task Call_ClientThrowsMidCall_RaisesDisconnectedAndReturnsDefaultWithoutThrowing()
    {
        var client = new FakeOpenRgbClient();
        var service = new LightingService(client, NullLogger<LightingService>.Instance);
        await service.ConnectAsync(TestContext.Current.CancellationToken);

        client.FailAllCallsWith = new IOException("connection reset");
        var disconnectedRaised = false;
        service.Disconnected += (_, _) => disconnectedRaised = true;

        var result = await service.SetAllColorAsync(RgbColor.White, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.True(disconnectedRaised);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task Dispose_DisposesTheUnderlyingClient()
    {
        var client = new FakeOpenRgbClient();
        var service = new LightingService(client, NullLogger<LightingService>.Instance);

        service.Dispose();

        Assert.Equal(1, client.DisposeCallCount);
    }
}
