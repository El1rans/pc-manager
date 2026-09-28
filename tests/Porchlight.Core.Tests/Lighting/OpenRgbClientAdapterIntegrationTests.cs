using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

/// <summary>
/// End-to-end tests against a real loopback socket (<see cref="FakeOpenRgbTcpServer"/>) and the
/// real vendored <c>OpenRGB.NET</c> client via <see cref="OpenRgbClientAdapter"/> - not
/// <see cref="FakeOpenRgbClient"/> - so the actual patched socket code (see
/// docs/upstream/openrgb-net.md) is what gets exercised for these two scenarios.
/// </summary>
public sealed class OpenRgbClientAdapterIntegrationTests
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ServerClosesGracefully_HeartbeatDetectsDisconnectWithinOneInterval()
    {
        using var server = new FakeOpenRgbTcpServer();
        using var cts = new CancellationTokenSource();
        var acceptTask = server.AcceptAsync(cts.Token);

        var adapter = new OpenRgbClientAdapter("127.0.0.1", server.Port, timeoutMs: 2000);
        using var service = new LightingService(
            adapter,
            NullLogger<LightingService>.Instance,
            callTimeout: TimeSpan.FromMilliseconds(300),
            heartbeatInterval: TimeSpan.FromMilliseconds(150));

        // Start connecting first - AcceptAsync only completes once something actually connects,
        // and nothing does until ConnectAsync's own Socket.Connect runs.
        var connectTask = service.ConnectAsync(TestContext.Current.CancellationToken);
        await acceptTask;
        var serveTask = server.ServeHandshakeThenHeartbeatsAsync(cts.Token);

        var connected = await connectTask;
        Assert.True(connected);

        var disconnectedRaised = false;
        service.Disconnected += (_, _) => disconnectedRaised = true;

        // Simulates the user closing OpenRGB, or stopping its SDK server: an orderly TCP shutdown,
        // not a crash. This is exactly the case the vendored library's own read loop used to treat
        // as "nothing happened" (see docs/upstream/openrgb-net.md) - only the heartbeat notices.
        server.CloseClientConnectionGracefully();

        await WaitUntilAsync(() => disconnectedRaised);

        Assert.False(service.IsConnected);

        cts.Cancel();
        try
        {
            await serveTask;
        }
        catch (OperationCanceledException)
        {
            // Expected: cts.Cancel() above.
        }
    }

    [Fact]
    public async Task ServerNeverAnswersHandshake_DisposeUnblocksThenFreshConnectSucceedsWithoutStaleSwap()
    {
        using var server = new FakeOpenRgbTcpServer();
        using var cts = new CancellationTokenSource();
        var acceptTask = server.AcceptAsync(cts.Token);

        var adapter = new OpenRgbClientAdapter("127.0.0.1", server.Port, timeoutMs: 60_000);

        // Start connecting first - AcceptAsync only completes once something actually connects.
        // The raw TCP connect succeeds almost immediately; it's the SDK-level handshake after that
        // which this server deliberately never answers, so Connect() gets stuck there.
        var stuckConnectTask = Task.Run(adapter.Connect, TestContext.Current.CancellationToken);
        await acceptTask;

        var finishedEarly = await Task.WhenAny(
            stuckConnectTask, Task.Delay(500, TestContext.Current.CancellationToken)) == stuckConnectTask;
        Assert.False(finishedEarly, "Connect() should still be blocked waiting for the handshake reply.");

        adapter.Dispose();

        // Dispose() must unblock the in-flight Connect() promptly - otherwise both the socket and
        // the thread blocked inside OpenRGB.NET's handshake read would leak for good.
        var unblocked = await Task.WhenAny(
            stuckConnectTask, Task.Delay(WaitBudget, TestContext.Current.CancellationToken)) == stuckConnectTask;
        Assert.True(unblocked, "Dispose() did not unblock the in-flight Connect().");
        Assert.False(adapter.Connected);

        // Reconnect with the *same* adapter instance, this time letting the (same) server actually
        // answer - proves the earlier, disposed-away attempt never resurfaced and swapped itself
        // into _client once it eventually unwound.
        var acceptTask2 = server.AcceptAsync(cts.Token);
        var connectTask = Task.Run(adapter.Connect, TestContext.Current.CancellationToken);
        await acceptTask2;
        var serveTask = server.ServeHandshakeThenHeartbeatsAsync(cts.Token);
        await connectTask;

        Assert.True(adapter.Connected);

        cts.Cancel();
        adapter.Dispose();
        try
        {
            await serveTask;
        }
        catch (OperationCanceledException)
        {
            // Expected: cts.Cancel() above.
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitBudget;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), "Condition was not met within the wait budget.");
    }
}
