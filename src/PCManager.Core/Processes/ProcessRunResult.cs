namespace PCManager.Core.Processes;

/// <summary>Outcome of a completed process run: exit code plus every stdout and stderr line, in order.</summary>
/// <param name="ExitCode">The process's exit code.</param>
/// <param name="StandardOutputLines">Every stdout line, split the same way <see cref="WingetOutputReader"/>
/// splits streamed output (spinner/progress redraws are not included as lines).</param>
/// <param name="StandardErrorLines">Every stderr line, split the same way as
/// <paramref name="StandardOutputLines"/>.</param>
public sealed record ProcessRunResult(
    int ExitCode, IReadOnlyList<string> StandardOutputLines, IReadOnlyList<string> StandardErrorLines);
