using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class UpdateOutcomeMemoryTests
{
    private static PersistedUpdateOutcome Outcome(string id, string version) => new()
    {
        PackageId = id,
        AvailableVersion = version,
        Kind = WingetOutcomeKind.ReinstallRequired,
        Title = "Needs a reinstall",
        Explanation = "Reinstalling will fix this.",
        ExitCode = unchecked((int)0x8A15008E),
        SuggestedAction = WingetSuggestedAction.Reinstall,
    };

    [Fact]
    public void Find_MatchingIdAndVersion_ReturnsEntry()
    {
        List<PersistedUpdateOutcome> entries = [Outcome("Some.Id", "2.0")];

        var found = UpdateOutcomeMemory.Find(entries, "Some.Id", "2.0");

        Assert.NotNull(found);
        Assert.Equal("Needs a reinstall", found!.Title);
    }

    [Fact]
    public void Find_IsCaseInsensitiveOnIdAndVersion()
    {
        List<PersistedUpdateOutcome> entries = [Outcome("Some.Id", "2.0")];

        Assert.NotNull(UpdateOutcomeMemory.Find(entries, "SOME.ID", "2.0"));
        Assert.NotNull(UpdateOutcomeMemory.Find(entries, "Some.Id", "2.0".ToUpperInvariant()));
    }

    [Fact]
    public void Find_DifferentAvailableVersion_ReturnsNull()
    {
        // "Clear the entry ... when the available version changes" - docs/specs/09-friendly-update-outcomes.md.
        List<PersistedUpdateOutcome> entries = [Outcome("Some.Id", "2.0")];

        var found = UpdateOutcomeMemory.Find(entries, "Some.Id", "3.0");

        Assert.Null(found);
    }

    [Fact]
    public void Find_NoMatch_ReturnsNull()
    {
        List<PersistedUpdateOutcome> entries = [];

        Assert.Null(UpdateOutcomeMemory.Find(entries, "Some.Id", "2.0"));
    }

    [Fact]
    public void Remember_NewPackage_AddsEntry()
    {
        List<PersistedUpdateOutcome> entries = [];

        UpdateOutcomeMemory.Remember(entries, Outcome("Some.Id", "2.0"));

        Assert.Single(entries);
    }

    [Fact]
    public void Remember_ExistingPackage_ReplacesRatherThanDuplicates()
    {
        List<PersistedUpdateOutcome> entries = [Outcome("Some.Id", "2.0")];

        UpdateOutcomeMemory.Remember(entries, Outcome("Some.Id", "3.0"));

        var entry = Assert.Single(entries);
        Assert.Equal("3.0", entry.AvailableVersion);
    }

    [Fact]
    public void Remember_OverMaxEntries_TrimsOldestFirst()
    {
        List<PersistedUpdateOutcome> entries = [];
        for (var i = 0; i < UpdateOutcomeMemory.MaxEntries; i++)
        {
            UpdateOutcomeMemory.Remember(entries, Outcome($"Id.{i}", "1.0"));
        }

        UpdateOutcomeMemory.Remember(entries, Outcome("Id.Overflow", "1.0"));

        Assert.Equal(UpdateOutcomeMemory.MaxEntries, entries.Count);
        Assert.Null(UpdateOutcomeMemory.Find(entries, "Id.0", "1.0")); // oldest was dropped
        Assert.NotNull(UpdateOutcomeMemory.Find(entries, "Id.Overflow", "1.0"));
    }

    [Fact]
    public void Forget_RemovesEntryForPackage()
    {
        List<PersistedUpdateOutcome> entries = [Outcome("Some.Id", "2.0"), Outcome("Other.Id", "1.0")];

        UpdateOutcomeMemory.Forget(entries, "Some.Id");

        Assert.Null(UpdateOutcomeMemory.Find(entries, "Some.Id", "2.0"));
        Assert.NotNull(UpdateOutcomeMemory.Find(entries, "Other.Id", "1.0"));
    }

    [Fact]
    public void Forget_UnknownPackage_IsNoOp()
    {
        List<PersistedUpdateOutcome> entries = [Outcome("Some.Id", "2.0")];

        UpdateOutcomeMemory.Forget(entries, "Nonexistent.Id");

        Assert.Single(entries);
    }

    [Fact]
    public void Prune_RemovesEntriesForPackagesNoLongerListed()
    {
        List<PersistedUpdateOutcome> entries = [Outcome("Still.Listed", "2.0"), Outcome("No.Longer.Listed", "1.0")];

        UpdateOutcomeMemory.Prune(entries, ["Still.Listed"]);

        Assert.NotNull(UpdateOutcomeMemory.Find(entries, "Still.Listed", "2.0"));
        Assert.Null(UpdateOutcomeMemory.Find(entries, "No.Longer.Listed", "1.0"));
    }

    [Fact]
    public void Prune_KeepsEverythingStillListed()
    {
        List<PersistedUpdateOutcome> entries = [Outcome("A", "1.0"), Outcome("B", "1.0")];

        UpdateOutcomeMemory.Prune(entries, ["A", "B"]);

        Assert.Equal(2, entries.Count);
    }
}
