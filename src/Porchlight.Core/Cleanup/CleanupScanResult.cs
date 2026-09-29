namespace Porchlight.Core.Cleanup;

/// <summary>The outcome of <see cref="ICleanupScanner.ScanAsync"/>.</summary>
/// <param name="Categories">One entry per scanned category, in the order given.</param>
public sealed record CleanupScanResult(IReadOnlyList<CleanupCategoryScan> Categories)
{
    public long TotalBytes => Categories.Sum(c => c.Bytes);
}
