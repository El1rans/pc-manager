using System.Text;
using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public class HttpRequestHeadReaderTests
{
    [Fact]
    public async Task ReadAsync_returns_the_head_without_the_blank_line_or_body()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: pc\r\n\r\nbody"));

        var (tooLarge, head) = await HttpRequestHeadReader.ReadAsync(stream, TestContext.Current.CancellationToken);

        Assert.False(tooLarge);
        Assert.Equal("GET / HTTP/1.1\r\nHost: pc", head);
    }

    [Fact]
    public async Task ReadAsync_finds_a_terminator_split_across_reads()
    {
        using var stream = new OneByteAtATimeStream(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n"));

        var (_, head) = await HttpRequestHeadReader.ReadAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal("GET / HTTP/1.1", head);
    }

    [Fact]
    public async Task ReadAsync_returns_null_when_the_client_closes_early()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost"));

        var (tooLarge, head) = await HttpRequestHeadReader.ReadAsync(stream, TestContext.Current.CancellationToken);

        Assert.False(tooLarge);
        Assert.Null(head);
    }

    [Fact]
    public async Task ReadAsync_flags_a_head_over_the_size_limit()
    {
        var huge = "GET / HTTP/1.1\r\nX-Filler: " + new string('a', WebConsoleOptions.MaxRequestHeadBytes) + "\r\n\r\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(huge));

        var (tooLarge, head) = await HttpRequestHeadReader.ReadAsync(stream, TestContext.Current.CancellationToken);

        Assert.True(tooLarge);
        Assert.Null(head);
    }

    /// <summary>Hands out one byte per read, like a slow network would at worst.</summary>
    private sealed class OneByteAtATimeStream(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
