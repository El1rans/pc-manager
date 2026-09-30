namespace Porchlight.Core.Elevation;

/// <summary>Runs a command as administrator (one UAC prompt) and waits for it, for the few
/// operations that need admin rights when Porchlight itself is not elevated - currently only
/// registering or removing the "start when I sign in" scheduled task.</summary>
public interface IElevatedCommandRunner
{
    /// <summary>
    /// Starts <paramref name="fileName"/> elevated with a hidden window and waits for it to exit.
    /// The output cannot be captured (the elevated process is started through the shell), so only
    /// the exit code is reported. A declined UAC prompt is a normal outcome, not an exception.
    /// </summary>
    Task<ElevatedRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

/// <summary>Outcome of <see cref="IElevatedCommandRunner.RunAsync"/>.</summary>
/// <param name="Declined">True when the user declined the UAC prompt; the command never ran.</param>
/// <param name="ExitCode">The command's exit code; meaningless when <paramref name="Declined"/>.</param>
public sealed record ElevatedRunResult(bool Declined, int ExitCode);
