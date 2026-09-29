using Porchlight.Core.Health;
using Xunit;

namespace Porchlight.Core.Tests.Health;

public class ProblemSummarizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private static HealthEventRecord Rec(string provider, int id, double daysAgo, params string[] props) =>
        new(provider, id, Now.AddDays(-daysAgo), props);

    [Fact]
    public void No_records_gives_no_rows()
    {
        Assert.Empty(ProblemSummarizer.Summarize([], Now));
    }

    [Fact]
    public void App_crashes_grouped_by_app_case_insensitively()
    {
        var rows = ProblemSummarizer.Summarize(
        [
            Rec("Application Error", 1000, 1, "chrome.exe"),
            Rec("Application Error", 1000, 2, "Chrome.exe"),
            Rec("Application Error", 1000, 3, "chrome.exe"),
            Rec("Application Error", 1000, 4, "chrome.exe"),
            Rec("Application Error", 1000, 5, "notepad.exe"),
        ], Now);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Chrome closed unexpectedly 4 times", rows[0].Title);
        Assert.Equal(4, rows[0].Count);
        Assert.Equal(Now.AddDays(-1), rows[0].LastOccurred);
        Assert.Equal("Notepad closed unexpectedly 1 time", rows[1].Title);
    }

    [Fact]
    public void Wer_appcrash_within_two_minutes_of_a_1000_is_not_double_counted()
    {
        var rows = ProblemSummarizer.Summarize(
        [
            Rec("Application Error", 1000, 1, "chrome.exe"),
            new HealthEventRecord("Windows Error Reporting", 1001, Now.AddDays(-1).AddSeconds(20),
                ["b", "1", "APPCRASH", "1", "c", "chrome.exe"]),
        ], Now);

        Assert.Equal(1, Assert.Single(rows).Count);
    }

    [Fact]
    public void Wer_appcrash_without_a_matching_1000_is_counted()
    {
        var rows = ProblemSummarizer.Summarize(
        [
            Rec("Windows Error Reporting", 1001, 1, "b", "1", "APPCRASH", "1", "c", "chrome.exe"),
            Rec("Windows Error Reporting", 1001, 2, "b", "1", "BEX64", "1", "c", "chrome.exe"),
        ], Now);

        Assert.Equal(1, Assert.Single(rows).Count);
    }

    [Fact]
    public void Blue_screens_counted()
    {
        var rows = ProblemSummarizer.Summarize(
        [
            Rec("Microsoft-Windows-WER-SystemErrorReporting", 1001, 3),
            Rec("Microsoft-Windows-WER-SystemErrorReporting", 1001, 6),
        ], Now);

        var row = Assert.Single(rows);
        Assert.Equal(ProblemCategory.BlueScreen, row.Category);
        Assert.Equal("Windows showed a blue screen and restarted 2 times", row.Title);
        Assert.NotNull(row.Advice);
    }

    [Fact]
    public void Kernel_power_and_6008_close_together_are_one_incident()
    {
        var rows = ProblemSummarizer.Summarize(
        [
            new HealthEventRecord("Microsoft-Windows-Kernel-Power", 41, Now.AddDays(-2), []),
            new HealthEventRecord("EventLog", 6008, Now.AddDays(-2).AddMinutes(1), []),
            new HealthEventRecord("Microsoft-Windows-Kernel-Power", 41, Now.AddDays(-9), []),
        ], Now);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.Count);
        Assert.Equal(ProblemCategory.UnexpectedShutdown, row.Category);
    }

    [Fact]
    public void Disk_errors_from_disk_and_ntfs()
    {
        var rows = ProblemSummarizer.Summarize(
        [
            Rec("disk", 7, 1),
            Rec("disk", 51, 2),
            Rec("disk", 153, 3),
            Rec("Ntfs", 55, 4),
            Rec("disk", 999, 4),
        ], Now);

        var row = Assert.Single(rows);
        Assert.Equal(4, row.Count);
        Assert.Equal("Windows had trouble reading or writing to a drive 4 times", row.Title);
    }

    [Fact]
    public void Failed_update_singular_wording()
    {
        var rows = ProblemSummarizer.Summarize([Rec("Microsoft-Windows-WindowsUpdateClient", 20, 1)], Now);
        Assert.Equal("A Windows update failed to install 1 time", Assert.Single(rows).Title);
    }

    [Fact]
    public void Records_older_than_30_days_are_ignored()
    {
        var rows = ProblemSummarizer.Summarize(
        [
            Rec("Application Error", 1000, 31, "chrome.exe"),
            Rec("Application Error", 1000, 29, "chrome.exe"),
        ], Now);

        Assert.Equal(1, Assert.Single(rows).Count);
    }

    [Fact]
    public void Serious_categories_sort_before_app_crashes()
    {
        var rows = ProblemSummarizer.Summarize(
        [
            Rec("Application Error", 1000, 1, "a.exe"),
            Rec("Application Error", 1000, 1, "a.exe"),
            Rec("Microsoft-Windows-WindowsUpdateClient", 20, 1),
            Rec("disk", 7, 1),
            Rec("Microsoft-Windows-WER-SystemErrorReporting", 1001, 1),
        ], Now);

        Assert.Equal(
            [ProblemCategory.BlueScreen, ProblemCategory.DiskError, ProblemCategory.FailedUpdate, ProblemCategory.AppCrash],
            rows.Select(r => r.Category).ToArray());
    }

    [Fact]
    public void Crash_record_missing_properties_is_skipped_not_thrown()
    {
        Assert.Empty(ProblemSummarizer.Summarize([Rec("Application Error", 1000, 1)], Now));
    }

    [Theory]
    [InlineData("msedge.exe", "Microsoft Edge")]
    [InlineData("C:\\x\\foo.exe", "Foo")]
    [InlineData("", "An app")]
    public void Friendly_app_names(string file, string expected)
    {
        Assert.Equal(expected, ProblemSummarizer.FriendlyAppName(file));
    }
}
