namespace Porchlight.Core.Startup;

/// <summary>Outcome of <see cref="IStartupInfoReader.Read"/>.</summary>
/// <param name="Records">Parsed records of the newest trace (empty when none could be read).</param>
/// <param name="AccessDenied">The StartupInfo folder exists but needs administrator rights.</param>
public sealed record StartupInfoReadResult(IReadOnlyList<StartupInfoRecord> Records, bool AccessDenied)
{
    public static StartupInfoReadResult Empty { get; } = new([], false);
}
