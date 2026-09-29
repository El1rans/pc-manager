using Porchlight.App.Features.Cleanup;
using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.App.Tests.Features.Cleanup;

public sealed class DiskInsightsTextFormatterTests
{
    [Theory]
    [InlineData(0.2, "Under 1%")]
    [InlineData(37.6, "38%")]
    [InlineData(100, "100%")]
    public void FormatPercent_IsPlain(double percent, string expected) =>
        Assert.Equal(expected, DiskInsightsTextFormatter.FormatPercent(percent));

    [Fact]
    public void FormatCouldntRead_IsEmptyWhenNothingWasSkipped_AndPluralises()
    {
        Assert.Equal(string.Empty, DiskInsightsTextFormatter.FormatCouldntRead(0));
        Assert.StartsWith("Couldn't read 1 folder (", DiskInsightsTextFormatter.FormatCouldntRead(1));
        Assert.StartsWith("Couldn't read 12 folders (", DiskInsightsTextFormatter.FormatCouldntRead(12));
    }

    [Fact]
    public void FormatSummary_UsesThousandsSeparators() =>
        Assert.Equal("1.0 GB in 48,210 files", DiskInsightsTextFormatter.FormatSummary(1024L * 1024 * 1024, 48_210));

    [Fact]
    public void FormatRemoveResult_MentionsLeftAloneFilesAndKeptCopies()
    {
        var text = DiskInsightsTextFormatter.FormatRemoveResult(
            new DuplicateRemoveResult(4, 2L * 1024 * 1024 * 1024, 1, 1, 1, false, []));

        Assert.Equal(
            "Moved 4 files (2.0 GB) to the Recycle Bin. 2 files were left alone because they were in use or had changed. One copy of each file was kept.",
            text);
    }

    [Fact]
    public void FormatRemoveResult_WhenStopped_SaysSo() =>
        Assert.StartsWith(
            "Stopped. Moved 1 file",
            DiskInsightsTextFormatter.FormatRemoveResult(new DuplicateRemoveResult(1, 1024, 0, 0, 0, true, [])));

    [Fact]
    public void FormatDuplicateProgress_NamesEachStageInPlainLanguage()
    {
        Assert.StartsWith("Looking through", DiskInsightsTextFormatter.FormatDuplicateProgress(new(DuplicateStage.Listing, 5, 0)));
        Assert.Equal(
            "Comparing files of the same size... 2 of 10",
            DiskInsightsTextFormatter.FormatDuplicateProgress(new(DuplicateStage.QuickCheck, 2, 10)));
        Assert.StartsWith("Confirming exact matches", DiskInsightsTextFormatter.FormatDuplicateProgress(new(DuplicateStage.Confirming, 1, 3)));
    }
}
