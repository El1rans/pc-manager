namespace Porchlight.Core.Cleanup;

/// <summary>Outcome of <see cref="IDuplicateRemover.RemoveAsync"/>.</summary>
/// <param name="FilesRemoved">Copies moved to the Recycle Bin.</param>
/// <param name="BytesFreed">Their combined size.</param>
/// <param name="FilesFailed">Copies the Recycle Bin refused (in use, protected).</param>
/// <param name="FilesChanged">Copies left alone because they (or the copy being kept) changed or
/// vanished since the scan.</param>
/// <param name="GroupsKeptOne">Groups where every copy was selected and the newest was spared.</param>
/// <param name="WasCancelled">True if the user stopped part-way.</param>
/// <param name="RemovedPaths">The paths that were moved to the Recycle Bin.</param>
public sealed record DuplicateRemoveResult(
    int FilesRemoved,
    long BytesFreed,
    int FilesFailed,
    int FilesChanged,
    int GroupsKeptOne,
    bool WasCancelled,
    IReadOnlyList<string> RemovedPaths);
