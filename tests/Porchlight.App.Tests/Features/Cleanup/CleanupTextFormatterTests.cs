using Porchlight.App.Features.Cleanup;
using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.App.Tests.Features.Cleanup;

public sealed class CleanupTextFormatterTests
{
    [Fact]
    public void FormatResult_ReportsFreedAndSkipped()
    {
        var text = CleanupTextFormatter.FormatResult(new CleanupRunResult(3_435_973_837, 10, 41, false, []));

        Assert.Equal("Freed 3.2 GB. 41 files were in use and left alone.", text);
    }

    [Fact]
    public void FormatResult_SingleSkippedFile_UsesSingular()
    {
        var text = CleanupTextFormatter.FormatResult(new CleanupRunResult(0, 0, 1, false, []));

        Assert.Contains("1 file was in use", text);
    }

    [Fact]
    public void FormatResult_NamesBlockedBrowsersAndCancellation()
    {
        var text = CleanupTextFormatter.FormatResult(new CleanupRunResult(0, 0, 0, true, ["Google Chrome"]));

        Assert.StartsWith("Stopped.", text);
        Assert.Contains("Close Google Chrome to clean its cache.", text);
    }

    [Theory]
    [InlineData(2021, 1, 1, "Installed 3 years ago")]
    [InlineData(2024, 7, 1, "Installed 2 months ago")]
    [InlineData(2023, 9, 1, "Installed 1 year ago")]
    [InlineData(2024, 8, 20, "Installed recently")]
    public void FormatInstalled_IsRelative(int year, int month, int day, string expected)
    {
        var today = new DateOnly(2024, 9, 1);

        Assert.Equal(expected, CleanupTextFormatter.FormatInstalled(new DateOnly(year, month, day), today));
    }

    [Fact]
    public void FormatInstalled_UnknownDate_IsEmpty()
    {
        Assert.Equal(string.Empty, CleanupTextFormatter.FormatInstalled(null, new DateOnly(2024, 9, 1)));
    }

    [Fact]
    public void FormatFreedSoFar_HiddenUntilSomethingWasFreed()
    {
        Assert.Equal(string.Empty, CleanupTextFormatter.FormatFreedSoFar(0));
        Assert.Equal("Porchlight has freed 1.0 KB so far", CleanupTextFormatter.FormatFreedSoFar(1024));
    }
}
