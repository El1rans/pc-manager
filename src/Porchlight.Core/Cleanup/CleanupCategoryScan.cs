namespace Porchlight.Core.Cleanup;

/// <summary>What a scan found for one category.</summary>
/// <param name="Id">The category.</param>
/// <param name="Bytes">Bytes that a clean would free right now.</param>
/// <param name="FileCount">Files that a clean would delete.</param>
/// <param name="BlockedPrograms">Programs that are running and so had their folders left out
/// (e.g. "Google Chrome"); the UI says "Close Google Chrome to clean its cache".</param>
public sealed record CleanupCategoryScan(
    CleanupCategoryId Id,
    long Bytes,
    int FileCount,
    IReadOnlyList<string> BlockedPrograms);
