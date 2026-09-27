using System.Net;
using System.Net.Sockets;

namespace PCManager.Core.Tests.Lighting;

/// <summary>
/// A minimal, real TCP server speaking just enough of the OpenRGB SDK wire protocol to let a real
/// <see cref="OpenRGB.NET.OpenRgbClient"/> (via <c>PCManager.Core.Lighting.OpenRgbClientAdapter</c>)
/// connect and run its heartbeat, so integration tests can exercise the actual vendored socket code
/// (see docs/upstream/openrgb-net.md) end to end instead of only through <see cref="FakeOpenRgbClient"/>.
/// </summary>
/// <remarks>
/// Wire format (see the vendored <c>PacketHeader</c>): a 16-byte header - "ORGB" (4 bytes),
/// deviceId (uint32 LE), command (uint32 LE), dataLength (uint32 LE) - followed by
/// <c>dataLength</c> payload bytes. This server does not need to understand a request's payload
/// (its length is already in the header), only recognize the command id to know whether/how to
/// reply.
/// </remarks>
internal sealed class FakeOpenRgbTcpServer : IDisposable
{
    private const uint CommandRequestControllerCount = 0;
    private const uint CommandRequestProtocolVersion = 40;
    private const uint CommandSetClientName = 50;
    private const uint SupportedProtocolVersion = 4;

    private readonly TcpListener _listener;
    private TcpClient? _accepted;
    private NetworkStream? _stream;

    public FakeOpenRgbTcpServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Accepts exactly one client connection. The connection itself (the TCP handshake)
    /// always succeeds instantly - it is the *protocol* handshake (RequestProtocolVersion) that a
    /// test controls via <see cref="ServeHandshakeAsync"/> or by simply never calling it.</summary>
    public async Task AcceptAsync(CancellationToken cancellationToken)
    {
        _accepted = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        _accepted.NoDelay = true;
        _stream = _accepted.GetStream();
    }

    /// <summary>Reads and answers exactly the handshake OpenRgbClient.Connect() performs
    /// (SetClientName, then RequestProtocolVersion), then keeps answering
    /// RequestControllerCount with zero (what LightingService's heartbeat sends) until
    /// <paramref name="cancellationToken"/> is cancelled or the client disconnects.</summary>
    public async Task ServeHandshakeThenHeartbeatsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var header = await ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
            if (header is null)
            {
                return;
            }

            await ReadExactAsync((int)header.Value.DataLength, cancellationToken).ConfigureAwait(false);

            switch (header.Value.Command)
            {
                case CommandRequestProtocolVersion:
                    await RespondAsync(CommandRequestProtocolVersion, SupportedProtocolVersion, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case CommandRequestControllerCount:
                    await RespondAsync(CommandRequestControllerCount, 0, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    // SetClientName and anything else this test doesn't need: no reply expected.
                    break;
            }
        }
    }

    /// <summary>Stops answering and closes the accepted socket in an orderly way (shutdown, then
    /// close) - simulates the user closing OpenRGB or stopping its SDK server.</summary>
    public void CloseClientConnectionGracefully()
    {
        _accepted?.Client.Shutdown(SocketShutdown.Both);
        _stream?.Dispose();
        _accepted?.Dispose();
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _accepted?.Dispose();
        _listener.Stop();
    }

    private async Task<(uint Command, uint DataLength)?> ReadHeaderAsync(CancellationToken cancellationToken)
    {
        var header = new byte[16];
        var read = 0;
        while (read < header.Length)
        {
            int n;
            try
            {
                n = await _stream!.ReadAsync(header.AsMemory(read), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
            {
                return null;
            }

            if (n == 0)
            {
                return null;
            }

            read += n;
        }

        var command = BitConverter.ToUInt32(header, 8);
        var dataLength = BitConverter.ToUInt32(header, 12);
        return (command, dataLength);
    }

    private async Task ReadExactAsync(int length, CancellationToken cancellationToken)
    {
        if (length == 0)
        {
            return;
        }

        var buffer = new byte[length];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await _stream!.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                return;
            }

            read += n;
        }
    }

    private async Task RespondAsync(uint command, uint value, CancellationToken cancellationToken)
    {
        var packet = new byte[16 + 4];
        "ORGB"u8.CopyTo(packet);
        BitConverter.GetBytes(0u).CopyTo(packet, 4); // deviceId
        BitConverter.GetBytes(command).CopyTo(packet, 8);
        BitConverter.GetBytes(4u).CopyTo(packet, 12); // dataLength
        BitConverter.GetBytes(value).CopyTo(packet, 16);

        await _stream!.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
    }
}
