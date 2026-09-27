using Xunit;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Tests.Monitoring;

public class RollingSeriesTests
{
    [Fact]
    public void Snapshot_is_empty_before_any_add()
    {
        var series = new RollingSeries(5);

        Assert.Empty(series.Snapshot());
        Assert.Equal(0, series.Min());
        Assert.Equal(0, series.Max());
        Assert.Equal(0, series.Average());
    }

    [Fact]
    public void Snapshot_returns_values_in_insertion_order_while_below_capacity()
    {
        var series = new RollingSeries(5);
        series.Add(1);
        series.Add(2);
        series.Add(3);

        Assert.Equal([1d, 2d, 3d], series.Snapshot());
    }

    [Fact]
    public void Snapshot_drops_oldest_once_full()
    {
        var series = new RollingSeries(3);
        series.Add(1);
        series.Add(2);
        series.Add(3);
        series.Add(4);
        series.Add(5);

        Assert.Equal([3d, 4d, 5d], series.Snapshot());
    }

    [Fact]
    public void Count_never_exceeds_capacity()
    {
        var series = new RollingSeries(3);
        for (var i = 0; i < 10; i++)
        {
            series.Add(i);
        }

        Assert.Equal(3, series.Count);
    }

    [Fact]
    public void Min_max_average_reflect_retained_window_only()
    {
        var series = new RollingSeries(3);
        series.Add(10);
        series.Add(20);
        series.Add(30);
        series.Add(100); // drops the 10

        Assert.Equal(20, series.Min());
        Assert.Equal(100, series.Max());
        Assert.Equal(50, series.Average());
    }
}
