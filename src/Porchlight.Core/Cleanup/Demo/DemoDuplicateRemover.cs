#if DEBUG
namespace Porchlight.Core.Cleanup.Demo;

/// <summary>DEBUG-only fake <see cref="IDuplicateRemover"/> that touches no file - see
/// <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoDuplicateRemover : IDuplicateRemover
{
    public Task<DuplicateRemoveResult> RemoveAsync(
        IReadOnlyList<DuplicateGroup> groups, IReadOnlyCollection<string> selectedPaths, CancellationToken cancellationToken)
    {
        var bytes = groups.Sum(group => group.FileBytes * group.Files.Count(file => selectedPaths.Contains(file.FullPath)));
        return Task.FromResult(new DuplicateRemoveResult(selectedPaths.Count, bytes, 0, 0, 0, WasCancelled: false, selectedPaths.ToList()));
    }
}
#endif
