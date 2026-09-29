namespace Porchlight.Core.Cleanup;

/// <summary>One folder a <see cref="CleanupCategory"/> cleans the contents of.</summary>
/// <param name="Path">Absolute folder path. Its contents are cleaned; the folder itself never is.</param>
/// <param name="RequiresAdmin">True if this folder can only be cleaned while elevated. It is skipped
/// (not an error) when Porchlight is not elevated.</param>
/// <param name="BlockingProcessName">Name (no ".exe") of a process that must not be running while
/// this folder is cleaned (a browser and its cache), or null.</param>
/// <param name="BlockingProcessDisplayName">Friendly name of that program, for the "Close X to clean
/// its cache" message.</param>
public sealed record CleanupRoot(
    string Path,
    bool RequiresAdmin = false,
    string? BlockingProcessName = null,
    string? BlockingProcessDisplayName = null);
