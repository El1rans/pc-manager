using Porchlight.Core.Network;
using Xunit;

namespace Porchlight.Core.Tests.Network;

public class NetworkAppUsageAggregatorTests
{
    private static string? Name(int pid) => pid switch
    {
        1 or 2 => "Browser",
        3 => "Mail",
        4 => "browser",
        _ => null,
    };

    [Fact]
    public void Groups_by_name_case_insensitively_and_sorts_busiest_first()
    {
        var connections = new List<NetworkConnection>
        {
            new(3), new(1), new(1), new(2), new(4),
        };

        var result = NetworkAppUsageAggregator.Aggregate(connections, Name);

        Assert.Equal(2, result.Count);
        Assert.Equal(4, result[0].ConnectionCount);
        Assert.Equal("Mail", result[1].Name);
    }

    [Fact]
    public void Drops_processes_with_no_name()
    {
        var result = NetworkAppUsageAggregator.Aggregate([new NetworkConnection(99)], Name);
        Assert.Empty(result);
    }

    [Fact]
    public void Ties_are_ordered_by_name()
    {
        var result = NetworkAppUsageAggregator.Aggregate([new NetworkConnection(3), new NetworkConnection(1)], Name);
        Assert.Equal(["Browser", "Mail"], result.Select(a => a.Name));
    }
}
