namespace PCManager.Core.Processes;

/// <summary>
/// Runs an external process (winget, an AnyDesk CLI verb, etc.) with its stdout streamed back
/// line-by-line, without ever showing a console window or blocking the calling thread.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Starts <paramref name="fileName"/> with <paramref name="arguments"/>, streams every real
    /// output line to <paramref name="onLine"/> as it is produced (a redrawn progress/spinner line
    /// goes to <paramref name="onProgress"/> instead - see <see cref="WingetOutputReader"/>), and
    /// returns once the process exits or <paramref name="cancellationToken"/> is cancelled (in
    /// which case the process is killed).
    /// </summary>
    /// <param name="fileName">Executable to launch (resolved via PATH if not rooted).</param>
    /// <param name="arguments">Arguments, passed through <c>ArgumentList</c> so each one is quoted
    /// correctly regardless of embedded spaces.</param>
    /// <param name="onLine">Receives each complete stdout (and stderr) line as it arrives.</param>
    /// <param name="onProgress">Receives redrawn progress-bar/spinner text; may be null.</param>
    /// <param name="cancellationToken">Cancelling kills the process and throws
    /// <see cref="OperationCanceledException"/>.</param>
    Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IProgress<string>? onLine,
        IProgress<string>? onProgress,
        CancellationToken cancellationToken);
}
