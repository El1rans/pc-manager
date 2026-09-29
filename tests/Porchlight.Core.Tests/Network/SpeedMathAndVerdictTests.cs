using Porchlight.Core.Network;
using Xunit;

namespace Porchlight.Core.Tests.Network;

public class SpeedMathAndVerdictTests
{
    [Fact]
    public void ToMbps_converts_bytes_and_time()
    {
        // 12,500,000 bytes = 100 megabits over 2 seconds = 50 Mbps.
        Assert.Equal(50, SpeedMath.ToMbps(12_500_000, TimeSpan.FromSeconds(2)), 6);
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(1000, 0)]
    [InlineData(-5, 1000)]
    public void ToMbps_is_zero_without_data_or_time(long bytes, int ms) =>
        Assert.Equal(0, SpeedMath.ToMbps(bytes, TimeSpan.FromMilliseconds(ms)));

    [Fact]
    public void Median_handles_odd_even_and_empty()
    {
        Assert.Equal(3, SpeedMath.Median([5, 1, 3]));
        Assert.Equal(2.5, SpeedMath.Median([4, 1, 2, 3]));
        Assert.Equal(0, SpeedMath.Median([]));
    }

    [Theory]
    [InlineData(1.0, SpeedVerdictLevel.Poor)]
    [InlineData(5.0, SpeedVerdictLevel.Fair)]
    [InlineData(25.0, SpeedVerdictLevel.Good)]
    [InlineData(200.0, SpeedVerdictLevel.Excellent)]
    public void Verdict_levels_follow_download_speed(double mbps, SpeedVerdictLevel expected)
    {
        var verdict = SpeedVerdictDescriber.Describe(new SpeedTestResult(20, mbps, 20, DateTimeOffset.UnixEpoch));
        Assert.Equal(expected, verdict.Level);
    }

    [Fact]
    public void Good_speed_mentions_video_calls()
    {
        var verdict = SpeedVerdictDescriber.Describe(new SpeedTestResult(20, 30, 10, DateTimeOffset.UnixEpoch));
        Assert.Contains("video calls", verdict.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void High_delay_and_low_upload_are_called_out()
    {
        var verdict = SpeedVerdictDescriber.Describe(new SpeedTestResult(400, 30, 0.5, DateTimeOffset.UnixEpoch));
        Assert.Contains("delay", verdict.Detail, StringComparison.Ordinal);
        Assert.Contains("upload", verdict.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_measured_is_unknown()
    {
        var verdict = SpeedVerdictDescriber.Describe(new SpeedTestResult(null, null, null, DateTimeOffset.UnixEpoch));
        Assert.Equal(SpeedVerdictLevel.Unknown, verdict.Level);
    }
}
