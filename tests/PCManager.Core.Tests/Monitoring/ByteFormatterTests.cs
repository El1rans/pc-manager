using Xunit;
using PCManager.Core.Monitoring;

namespace PCManager.Core.Tests.Monitoring;

public class ByteFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1288490188.8, "1.2 GB")]
    [InlineData(1024L * 1024 * 1024, "1.0 GB")]
    public void FormatBytes_scales_to_the_nearest_unit(double bytes, string expected) =>
        Assert.Equal(expected, ByteFormatter.FormatBytes(bytes));

    [Fact]
    public void FormatBytes_clamps_negative_to_zero() =>
        Assert.Equal("0 B", ByteFormatter.FormatBytes(-100));

    [Fact]
    public void FormatByteRate_appends_per_second() =>
        Assert.Equal("1.2 MB/s", ByteFormatter.FormatByteRate(1258291.2));

    [Theory]
    [InlineData(0, "0 bps")]
    [InlineData(1_537_500d, "12.3 Mbps")]
    public void FormatBitRate_converts_bytes_to_bits(double bytesPerSecond, string expected) =>
        Assert.Equal(expected, ByteFormatter.FormatBitRate(bytesPerSecond));

    [Theory]
    [InlineData(0, 0, 0, "0m")]
    [InlineData(0, 0, 45, "45m")]
    [InlineData(0, 4, 12, "4h 12m")]
    [InlineData(3, 4, 12, "3d 4h 12m")]
    public void FormatDuration_drops_leading_zero_components(int days, int hours, int minutes, string expected) =>
        Assert.Equal(expected, ByteFormatter.FormatDuration(new TimeSpan(days, hours, minutes, 0)));

    [Fact]
    public void FormatDuration_clamps_negative_to_zero() =>
        Assert.Equal("0m", ByteFormatter.FormatDuration(TimeSpan.FromMinutes(-5)));
}
