using OpenRGB.NET;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

/// <summary>
/// The vendored read loop must not trust the peer's packet header (see docs/upstream/openrgb-net.md):
/// bad magic / oversized length end the connection, unknown commands are skipped.
/// </summary>
public sealed class OpenRgbConnectionHardeningTests
{
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task OversizedDataLength_ClosesConnectionInsteadOfAllocating()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = new FakeOpenRgbTcpServer();
        var acceptTask = server.AcceptAsync(ct);
        using var connection = new OpenRgbConnection();

        var connectTask = Task.Run(() => connection.Connect("test", "127.0.0.1", server.Port, 2000), ct);
        await acceptTask.WaitAsync(HangGuard, ct);
        await server.ReadUntilHandshakeRequestAsync(ct);

        await server.SendRawAsync(FakeOpenRgbTcpServer.BuildPacket(40, uint.MaxValue, []), ct);

        // The read loop treats this as a protocol error and completes the reply queues, so the
        // pending handshake request fails fast instead of hanging or attempting a 4 GiB allocation.
        await Assert.ThrowsAsync<InvalidOperationException>(() => connectTask.WaitAsync(HangGuard, ct));
    }

    [Fact]
    public async Task BadMagic_ClosesConnection()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = new FakeOpenRgbTcpServer();
        var acceptTask = server.AcceptAsync(ct);
        using var connection = new OpenRgbConnection();

        var connectTask = Task.Run(() => connection.Connect("test", "127.0.0.1", server.Port, 2000), ct);
        await acceptTask.WaitAsync(HangGuard, ct);
        await server.ReadUntilHandshakeRequestAsync(ct);

        await server.SendRawAsync(FakeOpenRgbTcpServer.BuildPacket(40, 0, [], validMagic: false), ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => connectTask.WaitAsync(HangGuard, ct));
    }

    [Fact]
    public async Task UnknownCommand_IsSkippedAndLaterRepliesStillWork()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = new FakeOpenRgbTcpServer();
        var acceptTask = server.AcceptAsync(ct);
        using var connection = new OpenRgbConnection();

        var connectTask = Task.Run(() => connection.Connect("test", "127.0.0.1", server.Port, 2000), ct);
        await acceptTask.WaitAsync(HangGuard, ct);
        await server.ReadUntilHandshakeRequestAsync(ct);

        byte[] junk = [1, 2, 3, 4, 5];
        await server.SendRawAsync(FakeOpenRgbTcpServer.BuildPacket(9999, (uint)junk.Length, junk), ct);
        await server.SendRawAsync(
            FakeOpenRgbTcpServer.BuildPacket(40, 4, BitConverter.GetBytes(4u)), ct);

        await connectTask.WaitAsync(HangGuard, ct);
        Assert.Equal(4u, connection.CurrentProtocolVersion.Number);
    }
}
