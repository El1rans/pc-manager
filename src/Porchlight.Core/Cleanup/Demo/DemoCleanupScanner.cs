#if DEBUG
namespace Porchlight.Core.Cleanup.Demo;

/// <summary>DEBUG-only fake <see cref="ICleanupScanner"/> reporting made-up sizes, used when
/// <c>PORCHLIGHT_DEMO_DATA=1</c> - see <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoCleanupScanner : ICleanupScanner
{
    public async Task<CleanupScanResult> ScanAsync(
        IReadOnlyList<CleanupCategory> categories,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

        var scans = categories
            .Select((category, index) => new CleanupCategoryScan(
                category.Id,
                (index + 1) * 310L * 1024 * 1024,
                (index + 1) * 120,
                []))
            .ToList();
        return new CleanupScanResult(scans);
    }
}
#endif
