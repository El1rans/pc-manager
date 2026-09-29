namespace Porchlight.Core.Network;

/// <summary>
/// Parses the buffers filled by <c>GetExtendedTcpTable</c> (<c>TCP_TABLE_OWNER_PID_ALL</c>):
/// a DWORD row count followed by <c>MIB_TCPROW_OWNER_PID</c> (IPv4) or
/// <c>MIB_TCP6ROW_OWNER_PID</c> (IPv6) rows. Keeps only established connections whose remote end is
/// not this computer itself.
/// </summary>
public static class TcpTableParser
{
    /// <summary>MIB_TCP_STATE_ESTAB.</summary>
    private const int StateEstablished = 5;

    private const int HeaderSize = 4;

    // MIB_TCPROW_OWNER_PID: state, localAddr, localPort, remoteAddr, remotePort, owningPid (DWORDs).
    private const int Row4Size = 24;
    private const int Row4StateOffset = 0;
    private const int Row4RemoteAddressOffset = 12;
    private const int Row4PidOffset = 20;

    // MIB_TCP6ROW_OWNER_PID: localAddr[16], localScope, localPort, remoteAddr[16], remoteScope,
    // remotePort, state, owningPid.
    private const int Row6Size = 56;
    private const int Row6RemoteAddressOffset = 24;
    private const int Row6StateOffset = 48;
    private const int Row6PidOffset = 52;
    private const int Ipv6AddressSize = 16;

    private const byte LoopbackFirstByteV4 = 127;

    /// <summary>Parses <paramref name="table"/>; a truncated or empty buffer yields fewer rows,
    /// never an exception.</summary>
    public static IReadOnlyList<NetworkConnection> Parse(ReadOnlySpan<byte> table, bool ipv6)
    {
        var rowSize = ipv6 ? Row6Size : Row4Size;
        if (table.Length < HeaderSize)
        {
            return [];
        }

        var declared = BitConverter.ToUInt32(table[..HeaderSize]);
        var available = (table.Length - HeaderSize) / rowSize;
        var count = (int)Math.Min(declared, (uint)available);

        var result = new List<NetworkConnection>();
        for (var i = 0; i < count; i++)
        {
            var row = table.Slice(HeaderSize + (i * rowSize), rowSize);
            var state = BitConverter.ToInt32(row[(ipv6 ? Row6StateOffset : Row4StateOffset)..]);
            if (state != StateEstablished)
            {
                continue;
            }

            var remote = ipv6
                ? row.Slice(Row6RemoteAddressOffset, Ipv6AddressSize)
                : row.Slice(Row4RemoteAddressOffset, 4);
            if (IsLoopback(remote, ipv6))
            {
                continue;
            }

            var pid = BitConverter.ToInt32(row[(ipv6 ? Row6PidOffset : Row4PidOffset)..]);
            result.Add(new NetworkConnection(pid));
        }

        return result;
    }

    private static bool IsLoopback(ReadOnlySpan<byte> address, bool ipv6)
    {
        if (!ipv6)
        {
            return address[0] == LoopbackFirstByteV4;
        }

        // ::1
        for (var i = 0; i < Ipv6AddressSize - 1; i++)
        {
            if (address[i] != 0)
            {
                return false;
            }
        }

        return address[Ipv6AddressSize - 1] == 1;
    }
}
