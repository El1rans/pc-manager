#if DEBUG
namespace Porchlight.Core.Cleanup.Demo;

/// <summary>DEBUG-only fake <see cref="ICleanupRunner"/> that deletes nothing and reports made-up
/// results - see <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoCleanupRunner : ICleanupRunner
{
    public async Task<CleanupRunResult> CleanAsync(
        IReadOnlyList<CleanupCategory> categories,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
        var bytes = categories.Count * 310L * 1024 * 1024;
        return new CleanupRunResult(bytes, categories.Count * 120, 41, WasCancelled: false, []);
    }
}
#endif
