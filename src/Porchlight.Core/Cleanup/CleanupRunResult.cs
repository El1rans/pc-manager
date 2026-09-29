namespace Porchlight.Core.Cleanup;

/// <summary>The outcome of <see cref="ICleanupRunner.CleanAsync"/>.</summary>
/// <param name="BytesFreed">Bytes actually freed.</param>
/// <param name="FilesDeleted">Files removed.</param>
/// <param name="FilesSkipped">Files left alone because they were in use or protected. Normal, never
/// an error.</param>
/// <param name="WasCancelled">True if the user stopped the clean part-way; the other figures cover
/// what was done up to that point.</param>
/// <param name="BlockedPrograms">Programs that were running, so their caches were not cleaned.</param>
public sealed record CleanupRunResult(
    long BytesFreed,
    int FilesDeleted,
    int FilesSkipped,
    bool WasCancelled,
    IReadOnlyList<string> BlockedPrograms);
