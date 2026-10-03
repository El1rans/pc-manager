using Porchlight.Core.Safety;
using Xunit;

namespace Porchlight.Core.Tests.Safety;

public sealed class UpdateHistorySummarizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static UpdateHistoryEntry Entry(int daysAgo, UpdateHistoryResult result, string title = "2026-09 Cumulative Update") =>
        new(Now.AddDays(-daysAgo), title, result);

    [Fact]
    public void Summarize_PicksNewestSuccess()
    {
        var summary = UpdateHistorySummarizer.Summarize(
            [Entry(40, UpdateHistoryResult.Succeeded), Entry(5, UpdateHistoryResult.SucceededWithErrors), Entry(20, UpdateHistoryResult.Succeeded)],
            Now);

        Assert.Equal(Now.AddDays(-5), summary.LastInstalled);
        Assert.Equal(0, summary.RecentFailures);
    }

    [Fact]
    public void Summarize_IgnoresDefinitionUpdates()
    {
        var summary = UpdateHistorySummarizer.Summarize(
            [
                Entry(0, UpdateHistoryResult.Succeeded, "Security Intelligence Update for Microsoft Defender Antivirus - KB2267602"),
                Entry(30, UpdateHistoryResult.Succeeded),
            ],
            Now);

        Assert.Equal(Now.AddDays(-30), summary.LastInstalled);
    }

    [Fact]
    public void Summarize_CountsOnlyRecentFailures()
    {
        var summary = UpdateHistorySummarizer.Summarize(
            [Entry(2, UpdateHistoryResult.Failed), Entry(10, UpdateHistoryResult.Failed), Entry(31, UpdateHistoryResult.Failed), Entry(3, UpdateHistoryResult.Aborted)],
            Now);

        Assert.Equal(2, summary.RecentFailures);
        Assert.Null(summary.LastInstalled);
    }

    [Fact]
    public void Summarize_Empty_HasNothing()
    {
        var summary = UpdateHistorySummarizer.Summarize([], Now);

        Assert.Null(summary.LastInstalled);
        Assert.Equal(0, summary.RecentFailures);
    }

    [Fact]
    public void Verdict_RecentInstall_IsGood()
    {
        var status = UpdateVerdictBuilder.Build(new UpdateHistorySummary(Now.AddDays(-9), 0), false, Now);

        Assert.Equal(UpdateVerdictBuilder.Recent, status.Verdict);
        Assert.Equal(SafetyLevel.Good, status.Level);
    }

    [Fact]
    public void Verdict_RestartPending_Wins()
    {
        var status = UpdateVerdictBuilder.Build(new UpdateHistorySummary(Now.AddDays(-1), 5), true, Now);

        Assert.Equal(UpdateVerdictBuilder.RestartNeeded, status.Verdict);
        Assert.Equal(SafetyLevel.Attention, status.Level);
    }

    [Fact]
    public void Verdict_ManyFailures_KeepsFailing()
    {
        var status = UpdateVerdictBuilder.Build(
            new UpdateHistorySummary(Now.AddDays(-9), UpdateVerdictBuilder.FailureConcernThreshold), false, Now);

        Assert.Equal(UpdateVerdictBuilder.KeepsFailing, status.Verdict);
    }

    [Fact]
    public void Verdict_FewFailures_IsStillGood()
    {
        var status = UpdateVerdictBuilder.Build(
            new UpdateHistorySummary(Now.AddDays(-9), UpdateVerdictBuilder.FailureConcernThreshold - 1), false, Now);

        Assert.Equal(SafetyLevel.Good, status.Level);
        Assert.Contains(status.Details, d => d.StartsWith("Failed attempts", StringComparison.Ordinal));
    }

    [Fact]
    public void Verdict_LongAgo_IsStale()
    {
        var status = UpdateVerdictBuilder.Build(new UpdateHistorySummary(Now.AddDays(-90), 0), false, Now);

        Assert.Equal("Windows hasn't updated for 90 days", status.Verdict);
        Assert.Equal(SafetyLevel.Attention, status.Level);
    }

    [Fact]
    public void Verdict_NoHistory_IsUnknown()
    {
        var status = UpdateVerdictBuilder.Build(new UpdateHistorySummary(null, 0), null, Now);

        Assert.Equal(UpdateVerdictBuilder.NothingRecorded, status.Verdict);
        Assert.Equal(SafetyLevel.Unknown, status.Level);
    }

    [Fact]
    public void Verdict_Unreadable_IsCouldNotCheck()
    {
        var status = UpdateVerdictBuilder.Build(null, null, Now);

        Assert.Equal(UpdateVerdictBuilder.CouldNotCheck, status.Verdict);
        Assert.Equal(SafetyLevel.Unknown, status.Level);
    }
}
