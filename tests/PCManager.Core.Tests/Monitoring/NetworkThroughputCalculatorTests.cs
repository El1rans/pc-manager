using Xunit;
using PCManager.Core.Monitoring;

namespace PCManager.Core.Tests.Monitoring;

public class NetworkThroughputCalculatorTests
{
    [Fact]
    public void CalculateDelta_sums_deltas_across_matching_adapters()
    {
        var previous = new Dictionary<string, NetworkAdapterSample>
        {
            ["eth0"] = new("eth0", BytesReceived: 1000, BytesSent: 500),
            ["wifi0"] = new("wifi0", BytesReceived: 2000, BytesSent: 1000),
        };
        var current = new List<NetworkAdapterSample>
        {
            new("eth0", BytesReceived: 1500, BytesSent: 600),
            new("wifi0", BytesReceived: 2200, BytesSent: 1300),
        };

        var (download, upload) = NetworkThroughputCalculator.CalculateDelta(previous, current);

        Assert.Equal(500 + 200, download);
        Assert.Equal(100 + 300, upload);
    }

    [Fact]
    public void CalculateDelta_a_newly_appeared_adapter_contributes_nothing_this_tick()
    {
        var previous = new Dictionary<string, NetworkAdapterSample>
        {
            ["eth0"] = new("eth0", BytesReceived: 1000, BytesSent: 500),
        };
        var current = new List<NetworkAdapterSample>
        {
            new("eth0", BytesReceived: 1100, BytesSent: 550),
            new("usb-tether", BytesReceived: 99_999, BytesSent: 88_888), // no baseline yet
        };

        var (download, upload) = NetworkThroughputCalculator.CalculateDelta(previous, current);

        Assert.Equal(100, download);
        Assert.Equal(50, upload);
    }

    [Fact]
    public void CalculateDelta_a_disappeared_adapter_is_simply_absent_from_the_total()
    {
        var previous = new Dictionary<string, NetworkAdapterSample>
        {
            ["eth0"] = new("eth0", BytesReceived: 1000, BytesSent: 500),
            ["wifi0"] = new("wifi0", BytesReceived: 2000, BytesSent: 1000),
        };
        var current = new List<NetworkAdapterSample>
        {
            new("eth0", BytesReceived: 1200, BytesSent: 700),
            // wifi0 unplugged/disabled between samples.
        };

        var (download, upload) = NetworkThroughputCalculator.CalculateDelta(previous, current);

        Assert.Equal(200, download);
        Assert.Equal(200, upload);
    }

    [Fact]
    public void CalculateDelta_a_reset_adapter_counter_clamps_to_zero_for_that_adapter_only()
    {
        var previous = new Dictionary<string, NetworkAdapterSample>
        {
            ["eth0"] = new("eth0", BytesReceived: 1_000_000, BytesSent: 500_000), // driver restarted...
            ["wifi0"] = new("wifi0", BytesReceived: 2000, BytesSent: 1000),
        };
        var current = new List<NetworkAdapterSample>
        {
            new("eth0", BytesReceived: 500, BytesSent: 200), // ...counters reset to near zero
            new("wifi0", BytesReceived: 2300, BytesSent: 1100),
        };

        var (download, upload) = NetworkThroughputCalculator.CalculateDelta(previous, current);

        // eth0's negative delta clamps to 0 rather than going negative or being dropped entirely;
        // wifi0's legitimate delta still counts.
        Assert.Equal(300, download);
        Assert.Equal(100, upload);
    }

    [Fact]
    public void CalculateDelta_of_empty_input_is_zero()
    {
        var (download, upload) = NetworkThroughputCalculator.CalculateDelta(
            new Dictionary<string, NetworkAdapterSample>(), []);

        Assert.Equal(0, download);
        Assert.Equal(0, upload);
    }
}
