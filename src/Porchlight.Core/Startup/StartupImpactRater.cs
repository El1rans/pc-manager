using System.Text.RegularExpressions;

namespace Porchlight.Core.Startup;

/// <summary>Rates a startup item from Windows' own startup trace, using Task Manager's documented
/// thresholds. Pure: no I/O.</summary>
public sealed partial class StartupImpactRater
{
    /// <summary>More CPU time than this is High.</summary>
    public const double HighCpuMs = 1000;

    /// <summary>At least this much CPU time is Medium.</summary>
    public const double MediumCpuMs = 300;

    /// <summary>More disk I/O than this (3 MB) is High.</summary>
    public const long HighDiskBytes = 3L * 1024 * 1024;

    /// <summary>At least this much disk I/O (300 KB) is Medium.</summary>
    public const long MediumDiskBytes = 300L * 1024;

    private const string ExtendedPathPrefix = @"\\?\";
    private const string NtPathPrefix = @"\??\";

    private readonly Dictionary<string, (double Cpu, long Disk)> _byPath = new(StringComparer.Ordinal);

    public StartupImpactRater(IEnumerable<StartupInfoRecord> records)
    {
        foreach (var record in records)
        {
            var key = Normalize(record.ImagePath);
            if (key.Length == 0)
            {
                continue;
            }

            _byPath[key] = _byPath.TryGetValue(key, out var existing)
                ? (Math.Max(existing.Cpu, record.CpuTimeMs), Math.Max(existing.Disk, record.DiskBytes))
                : (record.CpuTimeMs, record.DiskBytes);
        }
    }

    /// <summary>The impact of the program at <paramref name="executablePath"/>, or
    /// <see cref="StartupImpact.NotMeasured"/> when the trace has no record for it.</summary>
    public StartupImpact Rate(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) ||
            !_byPath.TryGetValue(Normalize(executablePath), out var usage))
        {
            return StartupImpact.NotMeasured;
        }

        return Classify(usage.Cpu, usage.Disk);
    }

    /// <summary>Task Manager's rule: either metric alone can raise the rating.</summary>
    public static StartupImpact Classify(double cpuTimeMs, long diskBytes)
    {
        if (cpuTimeMs > HighCpuMs || diskBytes > HighDiskBytes)
        {
            return StartupImpact.High;
        }

        return cpuTimeMs >= MediumCpuMs || diskBytes >= MediumDiskBytes ? StartupImpact.Medium : StartupImpact.Low;
    }

    /// <summary>Case-insensitive key that ignores the volume: environment variables expanded,
    /// <c>\\?\</c> / <c>\??\</c> / <c>\Device\HarddiskVolumeN</c> / drive-letter prefixes dropped.</summary>
    internal static string Normalize(string path)
    {
        var text = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')).Replace('/', '\\');
        foreach (var prefix in new[] { ExtendedPathPrefix, NtPathPrefix })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                text = text[prefix.Length..];
            }
        }

        text = DevicePrefix().Replace(text, string.Empty);
        if (text.Length >= 2 && text[1] == ':' && char.IsAsciiLetter(text[0]))
        {
            text = text[2..];
        }

        return text.ToLowerInvariant();
    }

    [GeneratedRegex(@"^\\device\\harddiskvolume\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DevicePrefix();
}
