using Porchlight.Core.Network;
using Xunit;

namespace Porchlight.Core.Tests.Network;

public class TcpTableParserTests
{
    private static byte[] Table4(params (int State, byte[] Remote, int Pid)[] rows)
    {
        var bytes = new List<byte>(BitConverter.GetBytes(rows.Length));
        foreach (var (state, remote, pid) in rows)
        {
            bytes.AddRange(BitConverter.GetBytes(state));
            bytes.AddRange(new byte[] { 192, 168, 1, 5 }); // local addr
            bytes.AddRange(new byte[4]);                    // local port
            bytes.AddRange(remote);
            bytes.AddRange(new byte[4]);                    // remote port
            bytes.AddRange(BitConverter.GetBytes(pid));
        }

        return [.. bytes];
    }

    private static byte[] Table6(int state, byte[] remote16, int pid)
    {
        var bytes = new List<byte>(BitConverter.GetBytes(1));
        bytes.AddRange(new byte[24]);   // local addr + scope + port
        bytes.AddRange(remote16);
        bytes.AddRange(new byte[8]);    // remote scope + port
        bytes.AddRange(BitConverter.GetBytes(state));
        bytes.AddRange(BitConverter.GetBytes(pid));
        return [.. bytes];
    }

    [Fact]
    public void Keeps_only_established_non_loopback_ipv4_rows()
    {
        var table = Table4(
            (5, [93, 184, 216, 34], 100),
            (5, [127, 0, 0, 1], 200),
            (2, [0, 0, 0, 0], 300),      // listening
            (5, [8, 8, 8, 8], 100));

        var result = TcpTableParser.Parse(table, ipv6: false);

        Assert.Equal([100, 100], result.Select(c => c.Pid));
    }

    [Fact]
    public void Parses_ipv6_rows_and_drops_loopback()
    {
        var remote = new byte[16];
        remote[0] = 0x20;
        remote[1] = 0x01;
        var loopback = new byte[16];
        loopback[15] = 1;

        Assert.Equal(42, Assert.Single(TcpTableParser.Parse(Table6(5, remote, 42), ipv6: true)).Pid);
        Assert.Empty(TcpTableParser.Parse(Table6(5, loopback, 42), ipv6: true));
        Assert.Empty(TcpTableParser.Parse(Table6(2, remote, 42), ipv6: true));
    }

    [Fact]
    public void Truncated_or_empty_buffers_do_not_throw()
    {
        Assert.Empty(TcpTableParser.Parse([], ipv6: false));
        Assert.Empty(TcpTableParser.Parse([1, 0], ipv6: false));

        // Header claims 3 rows but only one row's bytes are present.
        var one = Table4((5, [1, 2, 3, 4], 7));
        one[0] = 3;
        Assert.Single(TcpTableParser.Parse(one, ipv6: false));
    }
}
