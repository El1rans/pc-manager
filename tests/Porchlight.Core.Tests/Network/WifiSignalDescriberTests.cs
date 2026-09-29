using Porchlight.Core.Network;
using Xunit;

namespace Porchlight.Core.Tests.Network;

public class WifiSignalDescriberTests
{
    [Theory]
    [InlineData(100, 4, "Excellent")]
    [InlineData(75, 4, "Excellent")]
    [InlineData(74, 3, "Good")]
    [InlineData(50, 3, "Good")]
    [InlineData(49, 2, "Fair")]
    [InlineData(25, 2, "Fair")]
    [InlineData(24, 1, "Weak")]
    [InlineData(1, 1, "Weak")]
    [InlineData(0, 0, "No signal")]
    [InlineData(-10, 0, "No signal")]
    [InlineData(250, 4, "Excellent")]
    public void Maps_percent_to_bars_and_words(int percent, int bars, string label)
    {
        var signal = WifiSignalDescriber.Describe(percent);
        Assert.Equal(bars, signal.Bars);
        Assert.Equal(label, signal.Label);
    }
}
