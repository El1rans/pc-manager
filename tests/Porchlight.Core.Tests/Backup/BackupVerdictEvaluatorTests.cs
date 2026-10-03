using Porchlight.Core.Backup;
using Xunit;

namespace Porchlight.Core.Tests.Backup;

public sealed class BackupVerdictEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly OneDriveStatus OneDriveNone = new(true, true, false, false, false, null);
    private static readonly OneDriveStatus OneDriveFull = new(true, true, true, true, true, null);

    private static BackupSnapshot Snapshot(
        FileHistoryStatus? fileHistory, OneDriveStatus? oneDrive, params string[] tools) =>
        new(fileHistory, oneDrive, tools);

    private static FileHistoryStatus History(double daysAgo, bool enabled = true) =>
        new(true, enabled, Now.AddDays(-daysAgo));

    [Fact]
    public void RecentFileHistoryBackup_IsGood()
    {
        var result = BackupVerdictEvaluator.Evaluate(
            Snapshot(History(2), OneDriveNone), Now);

        Assert.Equal(BackupVerdict.Good, result.Verdict);
        Assert.Null(result.Nudge);
        Assert.Contains("2 days ago", string.Join(' ', result.Lines));
    }

    [Fact]
    public void FileHistoryBackup_JustInsideTheThreshold_IsGood()
    {
        var almost = new FileHistoryStatus(
            true, true, Now - TimeSpan.FromDays(BackupVerdictEvaluator.RecentBackupDays) + TimeSpan.FromMinutes(1));

        Assert.Equal(BackupVerdict.Good, BackupVerdictEvaluator.Evaluate(Snapshot(almost, OneDriveNone), Now).Verdict);
    }

    [Fact]
    public void FileHistoryBackup_AtTheThreshold_IsWarning()
    {
        var exactly = new FileHistoryStatus(true, true, Now - TimeSpan.FromDays(BackupVerdictEvaluator.RecentBackupDays));

        var result = BackupVerdictEvaluator.Evaluate(Snapshot(exactly, OneDriveNone), Now);

        Assert.Equal(BackupVerdict.Warning, result.Verdict);
        Assert.Contains("last backup was", result.Headline);
        Assert.False(result.OfferFileHistory);
    }

    [Fact]
    public void ConfiguredButNeverRan_IsWarning()
    {
        var result = BackupVerdictEvaluator.Evaluate(
            Snapshot(new FileHistoryStatus(true, true, null), OneDriveNone), Now);

        Assert.Equal(BackupVerdict.Warning, result.Verdict);
        Assert.Equal("Your backup has not run yet.", result.Headline);
    }

    [Fact]
    public void ConfiguredButTurnedOffAndOld_IsWarning_AndOffersFileHistory()
    {
        var result = BackupVerdictEvaluator.Evaluate(
            Snapshot(History(90, enabled: false), OneDriveNone), Now);

        Assert.Equal(BackupVerdict.Warning, result.Verdict);
        Assert.True(result.OfferFileHistory);
        Assert.Contains("3 months ago", result.Headline);
    }

    [Fact]
    public void OneDriveCoveringDocumentsAndDesktop_IsGood_EvenWithoutFileHistory()
    {
        var oneDrive = new OneDriveStatus(true, true, true, true, false, null);

        var result = BackupVerdictEvaluator.Evaluate(Snapshot(FileHistoryStatus.NotSetUp, oneDrive), Now);

        Assert.Equal(BackupVerdict.Good, result.Verdict);
        Assert.Contains("protects Desktop and Documents, but not Pictures", string.Join(' ', result.Lines));
    }

    [Fact]
    public void OneDriveCoveringOnlyPictures_IsWarning()
    {
        var oneDrive = new OneDriveStatus(true, true, false, false, true, null);

        var result = BackupVerdictEvaluator.Evaluate(Snapshot(FileHistoryStatus.NotSetUp, oneDrive), Now);

        Assert.Equal(BackupVerdict.Warning, result.Verdict);
        Assert.Equal("OneDrive only protects some of your files.", result.Headline);
        Assert.True(result.OfferFileHistory);
    }

    [Fact]
    public void NothingConfigured_IsProblem()
    {
        var result = BackupVerdictEvaluator.Evaluate(
            Snapshot(FileHistoryStatus.NotSetUp, OneDriveStatus.NotInstalled), Now);

        Assert.Equal(BackupVerdict.Problem, result.Verdict);
        Assert.Equal("Nothing is backing up your files.", result.Headline);
        Assert.True(result.OfferFileHistory);
        Assert.NotNull(result.Nudge);
    }

    [Fact]
    public void OneDriveSignedOut_IsProblemAndSaysSo()
    {
        var result = BackupVerdictEvaluator.Evaluate(
            Snapshot(FileHistoryStatus.NotSetUp, new OneDriveStatus(true, false, false, false, false, null)), Now);

        Assert.Equal(BackupVerdict.Problem, result.Verdict);
        Assert.Contains("OneDrive is installed but not signed in.", result.Lines);
    }

    [Fact]
    public void Problem_WithOtherToolsFound_ListsThemWithoutClaimingTheyWork()
    {
        var result = BackupVerdictEvaluator.Evaluate(
            Snapshot(FileHistoryStatus.NotSetUp, OneDriveStatus.NotInstalled, "Dropbox", "Backblaze"), Now);

        Assert.Equal(BackupVerdict.Problem, result.Verdict);
        Assert.Equal(
            "Also found: Dropbox, Backblaze. Porchlight can't tell whether they are working.", result.OtherToolsNote);
    }

    [Fact]
    public void OtherToolsAlone_DoNotMakeTheVerdictGood()
    {
        var result = BackupVerdictEvaluator.Evaluate(
            Snapshot(FileHistoryStatus.NotSetUp, OneDriveStatus.NotInstalled, "Dropbox"), Now);

        Assert.NotEqual(BackupVerdict.Good, result.Verdict);
    }

    [Fact]
    public void Good_WithOtherTools_ShowsAlsoFound()
    {
        var result = BackupVerdictEvaluator.Evaluate(Snapshot(History(1), OneDriveNone, "Dropbox"), Now);

        Assert.Equal("Also found: Dropbox.", result.OtherToolsNote);
    }

    [Fact]
    public void UnknownSource_WithNothingElse_IsWarningNotProblem()
    {
        var result = BackupVerdictEvaluator.Evaluate(Snapshot(null, OneDriveStatus.NotInstalled), Now);

        Assert.Equal(BackupVerdict.Warning, result.Verdict);
        Assert.Contains("File History: couldn't check.", result.Lines);
    }

    [Fact]
    public void UnknownOneDrive_ButRecentFileHistory_IsStillGood()
    {
        var result = BackupVerdictEvaluator.Evaluate(Snapshot(History(0.5), null), Now);

        Assert.Equal(BackupVerdict.Good, result.Verdict);
        Assert.Contains("OneDrive: couldn't check.", result.Lines);
    }

    [Theory]
    [InlineData(0, "today")]
    [InlineData(1, "yesterday")]
    [InlineData(5, "5 days ago")]
    [InlineData(14, "2 weeks ago")]
    [InlineData(45, "6 weeks ago")]
    [InlineData(60, "2 months ago")]
    [InlineData(400, "13 months ago")]
    public void DescribeAge_IsPlain(int days, string expected) =>
        Assert.Equal(expected, BackupVerdictEvaluator.DescribeAge(TimeSpan.FromDays(days)));

    [Fact]
    public void DescribeAge_NegativeAge_IsToday() =>
        Assert.Equal("today", BackupVerdictEvaluator.DescribeAge(TimeSpan.FromHours(-3)));

    [Fact]
    public void Lines_NeverContainEmailOrPaths()
    {
        var result = BackupVerdictEvaluator.Evaluate(Snapshot(History(1), OneDriveFull), Now);

        Assert.DoesNotContain(result.Lines, line => line.Contains('@') || line.Contains('\\'));
    }
}
