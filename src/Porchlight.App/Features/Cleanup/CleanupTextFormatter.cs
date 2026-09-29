using System.Globalization;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Cleanup;

/// <summary>Plain-language text for the Free up space page, kept pure so it is unit-testable.</summary>
public static class CleanupTextFormatter
{
    private const int DaysPerMonth = 30;
    private const int DaysPerYear = 365;

    /// <summary>"Freed 3.2 GB. 41 files were in use and were left alone."</summary>
    public static string FormatResult(CleanupRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var parts = new List<string>
        {
            result.WasCancelled
                ? $"Stopped. Freed {ByteFormatter.FormatBytes(result.BytesFreed)} so far."
                : $"Freed {ByteFormatter.FormatBytes(result.BytesFreed)}.",
        };

        if (result.FilesSkipped > 0)
        {
            var noun = result.FilesSkipped == 1 ? "file was" : "files were";
            parts.Add($"{result.FilesSkipped.ToString("N0", CultureInfo.InvariantCulture)} {noun} in use and left alone.");
        }

        foreach (var program in result.BlockedPrograms)
        {
            parts.Add($"Close {program} to clean its cache.");
        }

        return string.Join(' ', parts);
    }

    /// <summary>"Installed 3 years ago", "Installed 2 months ago", "Installed recently", or empty.</summary>
    public static string FormatInstalled(DateOnly? installDate, DateOnly today)
    {
        if (installDate is not { } date)
        {
            return string.Empty;
        }

        var days = today.DayNumber - date.DayNumber;
        if (days >= DaysPerYear)
        {
            var years = days / DaysPerYear;
            return $"Installed {years} {(years == 1 ? "year" : "years")} ago";
        }

        if (days >= DaysPerMonth)
        {
            var months = days / DaysPerMonth;
            return $"Installed {months} {(months == 1 ? "month" : "months")} ago";
        }

        return "Installed recently";
    }

    /// <summary>"Modified 12 Mar 2024".</summary>
    public static string FormatModified(DateTime lastWriteUtc) =>
        "Modified " + lastWriteUtc.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"Porchlight has freed 12.4 GB so far".</summary>
    public static string FormatFreedSoFar(long totalBytes) =>
        totalBytes <= 0 ? string.Empty : $"Porchlight has freed {ByteFormatter.FormatBytes(totalBytes)} so far";
}
