using System.Globalization;

namespace Porchlight.Core.Safety;

/// <summary>Turns the update history into the verdict line. Pure.</summary>
public static class UpdateVerdictBuilder
{
    public const string RestartNeeded = "Restart your PC to finish updating";
    public const string KeepsFailing = "Windows updates keep failing";
    public const string Recent = "Windows installed updates recently";
    public const string CouldNotCheck = "Couldn't check Windows Update";
    public const string NothingRecorded = "No Windows updates recorded yet";

    /// <summary>Failed attempts in the recent window that count as "keeps failing".</summary>
    public const int FailureConcernThreshold = 3;

    /// <summary>Days without an install after which the PC is considered behind.</summary>
    public const int StaleAfterDays = 60;

    /// <param name="summary">The history summary, or null when the history could not be read.</param>
    /// <param name="rebootPending">Whether Windows is waiting for a restart, when known.</param>
    /// <param name="now">The current time.</param>
    public static WindowsUpdateStatus Build(UpdateHistorySummary? summary, bool? rebootPending, DateTimeOffset now)
    {
        if (summary is null)
        {
            return new WindowsUpdateStatus(CouldNotCheck, SafetyLevel.Unknown, null, 0, rebootPending, []);
        }

        var details = new List<string>();
        var daysSince = summary.LastInstalled is { } last ? (int)Math.Max(0, (now - last).TotalDays) : (int?)null;
        details.Add(summary.LastInstalled is { } date
            ? $"Last installed updates: {date.LocalDateTime.ToString("d MMMM yyyy", CultureInfo.CurrentCulture)} ({DaysAgo(daysSince ?? 0)})"
            : "No installed updates found in the history.");
        if (summary.RecentFailures > 0)
        {
            details.Add($"Failed attempts in the last {UpdateHistorySummarizer.RecentWindow.Days} days: {summary.RecentFailures}");
        }

        if (rebootPending == true)
        {
            details.Add("Windows is waiting for a restart.");
        }

        var (verdict, level) = Decide(summary, rebootPending, daysSince);
        return new WindowsUpdateStatus(verdict, level, summary.LastInstalled, summary.RecentFailures, rebootPending, details);
    }

    private static (string Verdict, SafetyLevel Level) Decide(UpdateHistorySummary summary, bool? rebootPending, int? daysSince)
    {
        if (rebootPending == true)
        {
            return (RestartNeeded, SafetyLevel.Attention);
        }

        if (summary.RecentFailures >= FailureConcernThreshold)
        {
            return (KeepsFailing, SafetyLevel.Attention);
        }

        if (daysSince is null)
        {
            return (NothingRecorded, SafetyLevel.Unknown);
        }

        return daysSince >= StaleAfterDays
            ? ($"Windows hasn't updated for {daysSince} days", SafetyLevel.Attention)
            : (Recent, SafetyLevel.Good);
    }

    private static string DaysAgo(int days) => days switch
    {
        0 => "today",
        1 => "1 day ago",
        _ => $"{days} days ago",
    };
}
