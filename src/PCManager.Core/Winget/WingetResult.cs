namespace PCManager.Core.Winget;

/// <summary>Outcome of running a single <c>winget upgrade</c>/<c>winget show</c> command.</summary>
/// <param name="ExitCode">The process's exit code - see <see cref="WingetExitCodes.Describe"/>.</param>
/// <param name="Lines">Every stdout line produced, in order (used by
/// <see cref="WingetExitCodes.MentionsRestart"/> to detect a "restart needed" success).</param>
public sealed record WingetResult(int ExitCode, IReadOnlyList<string> Lines);
