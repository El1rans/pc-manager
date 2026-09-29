namespace Porchlight.Core.Network;

/// <summary>Throughput arithmetic for the speed test.</summary>
public static class SpeedMath
{
    private const double BitsPerByte = 8;
    private const double BitsPerMegabit = 1_000_000;

    /// <summary>Megabits per second for <paramref name="bytes"/> moved in <paramref name="elapsed"/>
    /// (1 megabit = 1,000,000 bits). Zero when there is no data or no elapsed time.</summary>
    public static double ToMbps(long bytes, TimeSpan elapsed)
    {
        if (bytes <= 0 || elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        return bytes * BitsPerByte / BitsPerMegabit / elapsed.TotalSeconds;
    }

    /// <summary>Median of <paramref name="values"/>; zero for an empty list.</summary>
    public static double Median(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
