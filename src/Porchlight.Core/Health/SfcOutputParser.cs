using System.Globalization;
using System.Text.RegularExpressions;

namespace Porchlight.Core.Health;

/// <summary>Parses <c>sfc /scannow</c> output (English). Input may still contain the NULs of
/// UTF-16 text decoded as UTF-8; <see cref="RepairOutputCleaner"/> is applied here.</summary>
public static partial class SfcOutputParser
{
    private const int MaxPercent = 100;

    public static SfcOutcome Parse(IEnumerable<string> lines)
    {
        var text = RepairOutputCleaner.JoinNormalized(lines).ToLowerInvariant();

        // Order matters: "unable to fix" and "successfully repaired" both contain "found corrupt files".
        if (text.Contains("was unable to fix", StringComparison.Ordinal))
        {
            return SfcOutcome.CouldNotRepair;
        }

        if (text.Contains("successfully repaired", StringComparison.Ordinal))
        {
            return SfcOutcome.Repaired;
        }

        if (text.Contains("did not find any integrity violations", StringComparison.Ordinal))
        {
            return SfcOutcome.NoProblems;
        }

        if (text.Contains("repair pending", StringComparison.Ordinal))
        {
            return SfcOutcome.RebootPending;
        }

        if (text.Contains("could not perform the requested operation", StringComparison.Ordinal))
        {
            return SfcOutcome.CouldNotRun;
        }

        return SfcOutcome.Unknown;
    }

    /// <summary>Reads the percentage from "Verification 45% complete." style text.</summary>
    public static bool TryParseProgress(string? line, out int percent)
    {
        percent = 0;
        var match = ProgressPattern().Match(RepairOutputCleaner.Clean(line));
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        percent = Math.Clamp(value, 0, MaxPercent);
        return true;
    }

    [GeneratedRegex(@"(\d{1,3})\s*%\s*complete", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProgressPattern();
}
