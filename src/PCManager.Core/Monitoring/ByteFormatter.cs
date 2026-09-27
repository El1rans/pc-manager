using System.Globalization;

namespace PCManager.Core.Monitoring;

/// <summary>
/// Formats byte counts, byte/bit rates and durations for display. Invariant of the current culture:
/// always uses <see cref="CultureInfo.InvariantCulture"/> so numbers render the same regardless of
/// the machine's regional settings.
/// </summary>
public static class ByteFormatter
{
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];
    private static readonly string[] BitUnits = ["bps", "Kbps", "Mbps", "Gbps", "Tbps"];

    /// <summary>Formats a byte count, e.g. <c>1.2 GB</c>.</summary>
    public static string FormatBytes(double bytes) => FormatScaled(Math.Max(bytes, 0), 1024, ByteUnits);

    /// <summary>Formats a byte rate, e.g. <c>1.2 MB/s</c>.</summary>
    public static string FormatByteRate(double bytesPerSecond) => $"{FormatBytes(bytesPerSecond)}/s";

    /// <summary>Formats a network throughput in bits per second, e.g. <c>12.3 Mbps</c>. Takes a
    /// byte rate (as sampled from the network counters) and converts to bits.</summary>
    public static string FormatBitRate(double bytesPerSecond)
    {
        var bitsPerSecond = Math.Max(bytesPerSecond, 0) * 8;
        return FormatScaled(bitsPerSecond, 1000, BitUnits);
    }

    /// <summary>Formats a duration, e.g. <c>3d 4h 12m</c>. Drops leading zero components.</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var days = duration.Days;
        var hours = duration.Hours;
        var minutes = duration.Minutes;

        if (days > 0)
        {
            return $"{days}d {hours}h {minutes}m";
        }

        if (hours > 0)
        {
            return $"{hours}h {minutes}m";
        }

        return $"{minutes}m";
    }

    private static string FormatScaled(double value, double scale, string[] units)
    {
        var unitIndex = 0;
        while (value >= scale && unitIndex < units.Length - 1)
        {
            value /= scale;
            unitIndex++;
        }

        var unit = units[unitIndex];
        var formatted = unitIndex == 0
            ? Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
        return $"{formatted} {unit}";
    }
}
