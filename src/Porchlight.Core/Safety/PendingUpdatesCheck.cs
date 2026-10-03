namespace Porchlight.Core.Safety;

/// <summary>Result of an explicit "Check now" for waiting updates.</summary>
/// <param name="Outcome">Found, timed out or failed.</param>
/// <param name="Count">Number of updates waiting; only meaningful when <paramref name="Outcome"/> is Found.</param>
public sealed record PendingUpdatesCheck(PendingUpdatesOutcome Outcome, int Count)
{
    /// <summary>Plain sentence for the card.</summary>
    public string Message => Outcome switch
    {
        PendingUpdatesOutcome.Found when Count == 0 => "No updates are waiting.",
        PendingUpdatesOutcome.Found when Count == 1 => "1 update is waiting.",
        PendingUpdatesOutcome.Found => $"{Count} updates are waiting.",
        PendingUpdatesOutcome.TimedOut => "Windows Update is taking a long time. Try again later, or open Windows Update.",
        _ => "Couldn't check for waiting updates. Open Windows Update to see.",
    };
}
