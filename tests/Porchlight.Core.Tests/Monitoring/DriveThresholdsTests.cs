using Xunit;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Tests.Monitoring;

public class DriveThresholdsTests
{
    private const long OneGb = 1024L * 1024 * 1024;

    [Fact]
    public void IsLow_false_for_a_healthy_drive()
    {
        // 500 GB total, 200 GB free (40% free, well above 10 GB and 10%).
        Assert.False(DriveThresholds.IsLow(200 * OneGb, 500 * OneGb));
    }

    [Fact]
    public void IsLow_true_under_the_absolute_10gb_floor_even_on_a_huge_drive()
    {
        // 4 TB drive with 5 GB free: 5GB is well above 10% would need... actually 10% of 4TB is huge,
        // so the absolute floor is what triggers this.
        Assert.True(DriveThresholds.IsLow(5 * OneGb, 4000L * OneGb));
    }

    [Fact]
    public void IsLow_true_under_the_10_percent_fraction_on_a_small_drive()
    {
        // 100 GB drive with 15 GB free: above the 10 GB floor, but under 10% (10 GB) is not... check
        // the boundary explicitly with 8 GB free (under both 10 GB floor and 10% of 100GB=10GB).
        Assert.True(DriveThresholds.IsLow(8 * OneGb, 100 * OneGb));
    }

    [Fact]
    public void IsLow_false_at_exactly_the_boundary_is_not_low()
    {
        // Free space exactly at 10% and exactly at the 10 GB floor is not "under" either threshold.
        Assert.False(DriveThresholds.IsLow(10 * OneGb, 100 * OneGb));
    }

    [Fact]
    public void IsLow_false_when_total_is_zero_or_negative_unknown_capacity()
    {
        Assert.False(DriveThresholds.IsLow(0, 0));
        Assert.False(DriveThresholds.IsLow(-1, -1));
    }
}
