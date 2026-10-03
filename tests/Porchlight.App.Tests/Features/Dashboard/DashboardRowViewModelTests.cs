using Porchlight.App.Features.Dashboard;
using Porchlight.Core.Monitoring;
using Xunit;

namespace Porchlight.App.Tests.Features.Dashboard;

public sealed class DashboardRowViewModelTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(100, 50, false, DriveFillLevel.Normal)]
    [InlineData(100, 16, false, DriveFillLevel.Normal)]
    [InlineData(100, 15, false, DriveFillLevel.Filling)]
    [InlineData(100, 40, true, DriveFillLevel.Low)]
    public void Drive_FillLevel_FollowsUsedShareAndWindowsLowFlag(long totalGb, long freeGb, bool isLow, DriveFillLevel expected)
    {
        var row = new DriveRowViewModel(new DriveSnapshot("C:\\", null, "NTFS", totalGb * Gb, freeGb * Gb, isLow));

        Assert.Equal(expected, row.FillLevel);
    }

    [Theory]
    [InlineData("Demo Browser", "D")]
    [InlineData("7-Zip", "7")]
    [InlineData("  (helper)", "H")]
    [InlineData("---", "?")]
    public void Process_Initial_IsFirstLetterOrDigit(string name, string expected)
    {
        var row = new ProcessRowViewModel(new ProcessGroupSnapshot(name, 1, 0, 0));

        Assert.Equal(expected, row.Initial);
    }

    [Fact]
    public void Process_Hue_IsStablePerNameAndIgnoresCase()
    {
        Assert.Equal(ProcessRowViewModel.HueFor("Porchlight"), ProcessRowViewModel.HueFor("PORCHLIGHT"));
        Assert.Equal(ProcessRowViewModel.HueFor("Demo Browser"), new ProcessRowViewModel(new ProcessGroupSnapshot("Demo Browser", 2, 1, 1)).Hue);
    }

    [Fact]
    public void Process_HueFor_MatchesTheWebConsole()
    {
        // app.js hueFor() uses the same hash and hue order; these were checked against it.
        Assert.Equal(Porchlight.App.Controls.Hue.Coral, ProcessRowViewModel.HueFor("Porchlight"));
        Assert.Equal(Porchlight.App.Controls.Hue.Violet, ProcessRowViewModel.HueFor("Demo Browser"));
    }

    [Fact]
    public void UpdateMemoryShares_IsRelativeToTheLargestRow()
    {
        var rows = new[]
        {
            new ProcessRowViewModel(new ProcessGroupSnapshot("a", 1, 0, 400)),
            new ProcessRowViewModel(new ProcessGroupSnapshot("b", 1, 0, 100)),
            new ProcessRowViewModel(new ProcessGroupSnapshot("c", 1, 0, 0)),
        };

        ProcessRowViewModel.UpdateMemoryShares(rows);

        Assert.Equal([1.0, 0.25, 0.0], rows.Select(r => r.MemoryShare));
    }

    [Fact]
    public void UpdateMemoryShares_EmptyOrAllZero_DoesNotDivideByZero()
    {
        ProcessRowViewModel.UpdateMemoryShares([]);
        var row = new ProcessRowViewModel(new ProcessGroupSnapshot("a", 1, 0, 0));

        ProcessRowViewModel.UpdateMemoryShares([row]);

        Assert.Equal(0, row.MemoryShare);
    }
}
