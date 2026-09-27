namespace Porchlight.Core.Monitoring;

/// <summary>The "low disk space" rule, factored out as a pure function so it is unit-testable
/// without touching real drives.</summary>
public static class DriveThresholds
{
    /// <summary>A drive is low on space when free space is under this many bytes outright
    /// (protects small drives where 10% would be too lenient).</summary>
    public const long LowFreeBytesThreshold = 10L * 1024 * 1024 * 1024;

    /// <summary>Fraction of total capacity below which free space counts as low.</summary>
    public const double LowFreeFractionThreshold = 0.10;

    public static bool IsLow(long freeBytes, long totalBytes)
    {
        if (totalBytes <= 0)
        {
            return false;
        }

        return freeBytes < LowFreeBytesThreshold || freeBytes < totalBytes * LowFreeFractionThreshold;
    }
}
