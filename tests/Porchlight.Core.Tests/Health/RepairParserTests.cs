using System.Text;
using Porchlight.Core.Health;
using Xunit;

namespace Porchlight.Core.Tests.Health;

public class RepairParserTests
{
    /// <summary>What the process runner hands us for UTF-16 sfc output decoded as UTF-8: a NUL after every char.</summary>
    private static string Utf16AsUtf8(string text) =>
        Encoding.UTF8.GetString(Encoding.Unicode.GetBytes(text));

    [Fact]
    public void Cleaner_strips_nuls()
    {
        Assert.Equal("hello", RepairOutputCleaner.Clean(Utf16AsUtf8("hello")));
        Assert.Equal(string.Empty, RepairOutputCleaner.Clean("\0"));
        Assert.Equal(string.Empty, RepairOutputCleaner.Clean(null));
    }

    [Theory]
    [InlineData("Windows Resource Protection did not find any integrity violations.", SfcOutcome.NoProblems)]
    [InlineData("Windows Resource Protection found corrupt files and successfully repaired them.", SfcOutcome.Repaired)]
    [InlineData("Windows Resource Protection found corrupt files but was unable to fix some of them.", SfcOutcome.CouldNotRepair)]
    [InlineData("Windows Resource Protection could not perform the requested operation.", SfcOutcome.CouldNotRun)]
    [InlineData("There is a system repair pending which requires reboot to complete.  Restart Windows and run sfc again.", SfcOutcome.RebootPending)]
    [InlineData("something else entirely", SfcOutcome.Unknown)]
    public void Sfc_outcome_from_utf16_text(string sentence, SfcOutcome expected)
    {
        string[] lines = [Utf16AsUtf8("Beginning system scan."), Utf16AsUtf8(sentence), "\0"];
        Assert.Equal(expected, SfcOutputParser.Parse(lines));
    }

    [Fact]
    public void Sfc_sentence_wrapped_over_two_lines_still_matches()
    {
        Assert.Equal(
            SfcOutcome.CouldNotRepair,
            SfcOutputParser.Parse(["Windows Resource Protection found corrupt files but was", "unable to fix some of them."]));
    }

    [Fact]
    public void Sfc_empty_output_is_unknown()
    {
        Assert.Equal(SfcOutcome.Unknown, SfcOutputParser.Parse([]));
    }

    [Theory]
    [InlineData("Verification 45% complete.", 45)]
    [InlineData("Verification 100% complete.", 100)]
    public void Sfc_progress(string line, int expected)
    {
        Assert.True(SfcOutputParser.TryParseProgress(Utf16AsUtf8(line), out var percent));
        Assert.Equal(expected, percent);
    }

    [Fact]
    public void Sfc_progress_rejects_other_text()
    {
        Assert.False(SfcOutputParser.TryParseProgress("Beginning system scan.", out _));
        Assert.False(SfcOutputParser.TryParseProgress(null, out _));
    }

    [Theory]
    [InlineData("[==========================100.0%==========================]", 100)]
    [InlineData("[=====                      20.5%                           ]", 20)]
    [InlineData("[                           0.0%                           ]", 0)]
    public void Dism_progress(string line, int expected)
    {
        Assert.True(DismOutputParser.TryParseProgress(line, out var percent));
        Assert.Equal(expected, percent);
    }

    [Fact]
    public void Dism_progress_rejects_other_text()
    {
        Assert.False(DismOutputParser.TryParseProgress("The restore operation completed successfully.", out _));
    }

    public static TheoryData<int, string[], DismOutcome> DismOutcomeCases => new()
    {
        { 0, ["The restore operation completed successfully."], DismOutcome.Succeeded }, // Success on exit zero.
        { 740, ["Error: 740", "Elevated permissions are required."], DismOutcome.NeedsAdmin },
        { -2146498529, ["Error: 0x800f081f", "The source files could not be found."], DismOutcome.SourceNotFound },
        { 1, ["Error: 87"], DismOutcome.Failed }, // Any other failure.
    };

    [Theory]
    [MemberData(nameof(DismOutcomeCases))]
    public void Dism_outcome_from_exit_code_and_output(int exitCode, string[] lines, DismOutcome expected)
    {
        Assert.Equal(expected, DismOutputParser.Parse(exitCode, lines));
    }
    [Fact]
    public void Only_could_not_repair_offers_dism()
    {
        foreach (var outcome in Enum.GetValues<SfcOutcome>())
        {
            Assert.Equal(outcome == SfcOutcome.CouldNotRepair, RepairOutcomeDescriber.OffersDism(outcome));
            Assert.False(string.IsNullOrWhiteSpace(RepairOutcomeDescriber.Describe(outcome)));
        }

        foreach (var outcome in Enum.GetValues<DismOutcome>())
        {
            Assert.False(string.IsNullOrWhiteSpace(RepairOutcomeDescriber.Describe(outcome)));
        }
    }
}
