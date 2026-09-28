using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRGB.NET.Utils;

internal static class SocketExtensions
{
    public static void Connect(this Socket socket, string host, int port, int timeoutMs, CancellationToken cancellationToken)
    {
        var result = socket.ConnectAsync(host, port);
        Task.WaitAny([result], timeoutMs, cancellationToken);

        if (socket.Connected)
            return;

        socket.Close();
        throw new TimeoutException("Could not connect to OpenRGB");
    }

    public static async Task ReceiveAllAsync(this Socket socket, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var recv = 0;
        while (recv < buffer.Length)
        {
            var received = await socket.ReceiveAsync(buffer[recv..], SocketFlags.None, cancellationToken);

            // Porchlight patch: a 0-byte read means the remote end performed an orderly shutdown
            // (OpenRGB closed, or its SDK server was stopped). The original code silently `break`s
            // here, leaving the caller's buffer only partially filled - or, for the read loop's
            // reused header buffer, entirely untouched/stale - which looks exactly like a valid
            // packet was received. That makes OpenRgbConnection.ReadLoop re-parse the previous
            // header forever (Socket.Connected stays true after a graceful close), spinning a CPU
            // core and enqueueing a stream of phantom replies. See docs/upstream/openrgb-net.md.
            // Throwing here instead lets ReadLoop notice the close and exit cleanly.
            if (received == 0)
                throw new IOException("The remote OpenRGB server closed the connection.");

            recv += received;
        }
    }

    public static async Task SendAllAsync(this Socket socket, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var sent = 0;
        while (sent < buffer.Length)
        {
            var sentBytes = await socket.SendAsync(buffer[sent..], SocketFlags.None, cancellationToken);
            sent += sentBytes;
        }
    }

    public static void SendAll(this Socket socket, ReadOnlySpan<byte> buffer)
    {
        var sent = 0;
        while (sent < buffer.Length)
        {
            var sentBytes = socket.Send(buffer[sent..]);
            sent += sentBytes;
        }
    }
}