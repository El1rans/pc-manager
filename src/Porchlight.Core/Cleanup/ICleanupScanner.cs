namespace Porchlight.Core.Cleanup;

/// <summary>Measures how much each cleanup category could free, without deleting anything.</summary>
public interface ICleanupScanner
{
    /// <summary>Runs entirely on a background thread. Throws <see cref="OperationCanceledException"/>
    /// if <paramref name="cancellationToken"/> is cancelled.</summary>
    Task<CleanupScanResult> ScanAsync(
        IReadOnlyList<CleanupCategory> categories,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken);
}
