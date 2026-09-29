using Porchlight.Core.Alerts;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.Core.Tests.Alerts;

public sealed class UpdateCheckPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UpdateCheckSchedule.Daily, 23, false)]
    [InlineData(UpdateCheckSchedule.Daily, 24, true)]
    [InlineData(UpdateCheckSchedule.Weekly, 24 * 6, false)]
    [InlineData(UpdateCheckSchedule.Weekly, 24 * 7, true)]
    [InlineData(UpdateCheckSchedule.Never, 24 * 400, false)]
    public void IsDue_FollowsTheSchedule(UpdateCheckSchedule schedule, int hoursAgo, bool expected)
    {
        Assert.Equal(expected, UpdateCheckPolicy.IsDue(schedule, Now.AddHours(-hoursAgo), Now));
    }

    [Fact]
    public void NeverChecked_IsDue_UnlessNever()
    {
        Assert.True(UpdateCheckPolicy.IsDue(UpdateCheckSchedule.Daily, null, Now));
        Assert.False(UpdateCheckPolicy.IsDue(UpdateCheckSchedule.Never, null, Now));
    }

    [Fact]
    public void LastCheckInTheFuture_IsDue()
    {
        Assert.True(UpdateCheckPolicy.IsDue(UpdateCheckSchedule.Daily, Now.AddDays(2), Now));
    }
}
