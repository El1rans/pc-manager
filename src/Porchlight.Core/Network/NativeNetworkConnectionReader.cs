using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Network;

/// <inheritdoc cref="INetworkConnectionReader"/>
/// <remarks>Calls <c>GetExtendedTcpTable</c> in <c>iphlpapi.dll</c>. UDP is deliberately not read:
/// a bound UDP socket says nothing about actual traffic, so it would only clutter the list.</remarks>
public sealed class NativeNetworkConnectionReader : INetworkConnectionReader
{
    private const int AfInet = 2;
    private const int AfInet6 = 23;

    /// <summary>TCP_TABLE_OWNER_PID_ALL.</summary>
    private const int TcpTableOwnerPidAll = 5;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaxAttempts = 4;
    private const int GrowthSlackBytes = 4096;

    private readonly ILogger<NativeNetworkConnectionReader> _logger;

    public NativeNetworkConnectionReader(ILogger<NativeNetworkConnectionReader> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<NetworkConnection> ReadConnections()
    {
        try
        {
            var connections = new List<NetworkConnection>();
            foreach (var (family, ipv6) in new[] { (AfInet, false), (AfInet6, true) })
            {
                var table = FetchTcpTable(family);
                if (table is not null)
                {
                    connections.AddRange(TcpTableParser.Parse(table, ipv6));
                }
            }

            return connections;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or OutOfMemoryException)
        {
            _logger.LogDebug(ex, "Could not read the TCP connection table.");
            return [];
        }
    }

    private byte[]? FetchTcpTable(int addressFamily)
    {
        var size = 0;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var buffer = size == 0 ? IntPtr.Zero : Marshal.AllocHGlobal(size);
            try
            {
                var result = GetExtendedTcpTable(buffer, ref size, false, addressFamily, TcpTableOwnerPidAll, 0);
                if (result == 0 && buffer != IntPtr.Zero)
                {
                    var bytes = new byte[size];
                    Marshal.Copy(buffer, bytes, 0, size);
                    return bytes;
                }

                if (result != ErrorInsufficientBuffer)
                {
                    if (_logger.IsEnabled(LogLevel.Debug)) { _logger.LogDebug("GetExtendedTcpTable failed with {Error}.", result); }
                    return null;
                }

                size += GrowthSlackBytes;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        return null;
    }

#pragma warning disable SYSLIB1054 // Raw buffer API; the source generator adds nothing here.
    [DllImport("iphlpapi.dll", SetLastError = false)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int pdwSize,
        [MarshalAs(UnmanagedType.Bool)] bool bOrder,
        int ulAf,
        int tableClass,
        uint reserved);
#pragma warning restore SYSLIB1054
}
