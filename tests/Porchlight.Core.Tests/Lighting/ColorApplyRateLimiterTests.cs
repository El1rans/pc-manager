using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

public sealed class ColorApplyRateLimiterTests
{
    [Fact]
    public void TryAcquire_FirstCall_AlwaysSucceeds()
    {
        var limiter = new ColorApplyRateLimiter(TimeSpan.FromMilliseconds(50), () => DateTime.UnixEpoch);

        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_SecondCallWithinInterval_IsRefused()
    {
        var now = DateTime.UnixEpoch;
        var limiter = new ColorApplyRateLimiter(TimeSpan.FromMilliseconds(50), () => now);

        Assert.True(limiter.TryAcquire());
        now += TimeSpan.FromMilliseconds(10);

        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_AfterIntervalElapses_Succeeds()
    {
        var now = DateTime.UnixEpoch;
        var limiter = new ColorApplyRateLimiter(TimeSpan.FromMilliseconds(50), () => now);

        Assert.True(limiter.TryAcquire());
        now += TimeSpan.FromMilliseconds(51);

        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_RepeatedCallsFasterThanInterval_AllowsAtMostTheConfiguredRate()
    {
        var now = DateTime.UnixEpoch;
        var limiter = new ColorApplyRateLimiter(TimeSpan.FromMilliseconds(50), () => now);
        var allowed = 0;

        // 1 second of "mouse move" events fired every 5ms (200/s) must be throttled down to ~20/s.
        for (var i = 0; i < 200; i++)
        {
            if (limiter.TryAcquire())
            {
                allowed++;
            }

            now += TimeSpan.FromMilliseconds(5);
        }

        Assert.InRange(allowed, 19, 21);
    }

    [Fact]
    public void Reset_ClearsLastAcquiredTime_SoNextCallSucceedsImmediately()
    {
        var now = DateTime.UnixEpoch;
        var limiter = new ColorApplyRateLimiter(TimeSpan.FromMilliseconds(50), () => now);
        limiter.TryAcquire();

        limiter.Reset();

        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void Constructor_NonPositiveInterval_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorApplyRateLimiter(TimeSpan.Zero));
    }
}
