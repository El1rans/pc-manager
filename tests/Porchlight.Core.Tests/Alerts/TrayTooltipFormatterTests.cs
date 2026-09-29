using Porchlight.Core.Tray;
using Xunit;

namespace Porchlight.Core.Tests.Alerts;

public sealed class TrayTooltipFormatterTests
{
    [Fact]
    public void Format_ShowsAllStats()
    {
        var text = TrayTooltipFormatter.Format(new QuickStats(12.4, 48.0, 85L * 1024 * 1024 * 1024, "C:\\"));

        Assert.Equal("Porchlight - CPU 12%, memory 48%, C: 85.0 GB free", text);
    }

    [Fact]
    public void Format_SkipsUnavailableStats()
    {
        Assert.Equal("Porchlight - memory 48%", TrayTooltipFormatter.Format(new QuickStats(null, 48, null, null)));
        Assert.Equal("Porchlight", TrayTooltipFormatter.Format(new QuickStats(null, null, null, null)));
    }

    [Fact]
    public void Format_NeverExceedsTheTooltipLimit()
    {
        var text = TrayTooltipFormatter.Format(new QuickStats(100, 100, long.MaxValue, new string('X', 300)));

        Assert.True(text.Length <= TrayTooltipFormatter.MaxLength);
    }
}
