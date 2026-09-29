using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public class FreePortFinderTests
{
    [Fact]
    public void Returns_the_preferred_port_when_it_is_free()
    {
        Assert.Equal(8765, FreePortFinder.FindNearest(8765, _ => true));
    }

    [Fact]
    public void Tries_above_then_below_moving_outwards()
    {
        var tried = new List<int>();

        var found = FreePortFinder.FindNearest(8765, port =>
        {
            tried.Add(port);
            return port == 8763;
        });

        Assert.Equal(8763, found);
        Assert.Equal([8765, 8766, 8764, 8767, 8763], tried);
    }

    [Fact]
    public void Picks_the_next_port_up_when_only_the_preferred_one_is_busy()
    {
        Assert.Equal(8766, FreePortFinder.FindNearest(8765, port => port != 8765));
    }

    [Fact]
    public void Never_returns_a_port_below_the_allowed_range()
    {
        var found = FreePortFinder.FindNearest(WebConsoleOptions.LowestAllowedPort, port => port < WebConsoleOptions.LowestAllowedPort);

        Assert.Null(found);
    }

    [Fact]
    public void Never_checks_a_port_above_the_allowed_range()
    {
        var tried = new List<int>();

        FreePortFinder.FindNearest(WebConsoleOptions.HighestAllowedPort, port =>
        {
            tried.Add(port);
            return false;
        });

        Assert.DoesNotContain(tried, port => !WebConsoleOptions.IsAllowedPort(port));
    }

    [Fact]
    public void Gives_up_after_the_search_distance()
    {
        var tried = 0;

        var found = FreePortFinder.FindNearest(8765, _ =>
        {
            tried++;
            return false;
        });

        Assert.Null(found);
        Assert.Equal((FreePortFinder.MaxSearchDistance * 2) + 1, tried);
    }
}
