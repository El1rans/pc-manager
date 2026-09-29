using System.Globalization;
using System.Text.RegularExpressions;

namespace Porchlight.Core.Health;

/// <summary>Parses <c>DISM /Online /Cleanup-Image /RestoreHealth</c> output and exit code.</summary>
public static partial class DismOutputParser
{
    private const int ExitSuccess = 0;
    private const int ExitElevationRequired = 740;
    private const int MaxPercent = 100;

    public static DismOutcome Parse(int exitCode, IEnumerable<string> lines)
    {
        var text = RepairOutputCleaner.JoinNormalized(lines).ToLowerInvariant();

        if (exitCode == ExitSuccess)
        {
            return DismOutcome.Succeeded;
        }

        if (exitCode == ExitElevationRequired
            || text.Contains("elevated permissions", StringComparison.Ordinal)
            || text.Contains("error: 740", StringComparison.Ordinal))
        {
            return DismOutcome.NeedsAdmin;
        }

        if (text.Contains("source files could not be found", StringComparison.Ordinal)
            || text.Contains("0x800f081f", StringComparison.Ordinal)
            || text.Contains("0x800f0906", StringComparison.Ordinal)
            || text.Contains("0x800f0907", StringComparison.Ordinal)
            || text.Contains("0x800f0950", StringComparison.Ordinal))
        {
            return DismOutcome.SourceNotFound;
        }

        return DismOutcome.Failed;
    }

    /// <summary>Reads the percentage from a "[=====  45.0%  ]" style bar (the fraction is dropped).</summary>
    public static bool TryParseProgress(string? line, out int percent)
    {
        percent = 0;
        var match = ProgressPattern().Match(RepairOutputCleaner.Clean(line));
        if (!match.Success
            || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        percent = (int)Math.Clamp(Math.Floor(value), 0, MaxPercent);
        return true;
    }

    [GeneratedRegex(@"^\[[= ]*(\d{1,3}(?:\.\d+)?)%[= ]*\]$", RegexOptions.CultureInvariant)]
    private static partial Regex ProgressPattern();
}
