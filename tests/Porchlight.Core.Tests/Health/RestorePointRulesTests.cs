using Porchlight.Core.Health;
using Xunit;

namespace Porchlight.Core.Tests.Health;

public class RestorePointRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private static RestorePointInfo Point(TimeSpan ago) => new(1, "x", Now - ago);

    [Fact]
    public void Allowed_when_no_restore_points()
    {
        Assert.Null(RestorePointRules.NextAllowed(1440, [], Now));
        Assert.Null(RestorePointRules.NextAllowed(1440, null, Now));
    }

    [Fact]
    public void Blocked_within_24_hours()
    {
        var next = RestorePointRules.NextAllowed(1440, [Point(TimeSpan.FromHours(3))], Now);
        Assert.Equal(Now + TimeSpan.FromHours(21), next);
    }

    [Fact]
    public void Allowed_after_24_hours()
    {
        Assert.Null(RestorePointRules.NextAllowed(1440, [Point(TimeSpan.FromHours(25))], Now));
    }

    [Fact]
    public void Zero_frequency_means_no_limit()
    {
        Assert.Null(RestorePointRules.NextAllowed(0, [Point(TimeSpan.FromMinutes(1))], Now));
    }

    [Fact]
    public void Evaluate_protection_off()
    {
        var a = RestorePointRules.Evaluate(new RestorePointStatus(false, 1440, []), Now);
        Assert.False(a.CanCreate);
        Assert.Contains("turned off", a.Message);
    }

    [Fact]
    public void Evaluate_too_soon_explains_wait()
    {
        var a = RestorePointRules.Evaluate(new RestorePointStatus(true, 1440, [Point(TimeSpan.FromHours(3))]), Now);
        Assert.False(a.CanCreate);
        Assert.Contains("24 hours", a.Message);
        Assert.Contains("about 21 hours", a.Message);
    }

    [Fact]
    public void Evaluate_ready_when_unknown_protection_and_no_points()
    {
        Assert.True(RestorePointRules.Evaluate(new RestorePointStatus(null, 1440, null), Now).CanCreate);
    }

    [Fact]
    public void Was_created_detects_a_new_point_only()
    {
        Assert.True(RestorePointRules.WasCreated([Point(TimeSpan.FromSeconds(5))], Now));
        Assert.False(RestorePointRules.WasCreated([Point(TimeSpan.FromHours(3))], Now));
        Assert.False(RestorePointRules.WasCreated(null, Now));
    }

    [Theory]
    [InlineData(1440, "24 hours")]
    [InlineData(60, "1 hour")]
    [InlineData(90, "90 minutes")]
    public void Frequency_text(int minutes, string expected)
    {
        Assert.Equal(expected, RestorePointRules.DescribeFrequency(minutes));
    }

    [Fact]
    public void Wait_text()
    {
        Assert.Equal("less than an hour", RestorePointRules.DescribeWait(TimeSpan.FromMinutes(20)));
        Assert.Equal("about 1 hour", RestorePointRules.DescribeWait(TimeSpan.FromMinutes(70)));
        Assert.Equal("about 1 day", RestorePointRules.DescribeWait(TimeSpan.FromHours(24)));
    }

    [Fact]
    public void Error_text_is_plain_for_known_codes()
    {
        Assert.Contains("turned off", RestorePointRules.DescribeError(RestorePointRules.ErrorServiceDisabled));
        Assert.Contains("administrator", RestorePointRules.DescribeError(RestorePointRules.ErrorAccessDenied));
        Assert.False(string.IsNullOrWhiteSpace(RestorePointRules.DescribeError(-1)));
    }
}
