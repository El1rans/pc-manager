namespace Porchlight.Core.Safety;

/// <summary>How a "Check now" search for waiting updates ended.</summary>
public enum PendingUpdatesOutcome
{
    /// <summary>The search finished; see <see cref="PendingUpdatesCheck.Count"/>.</summary>
    Found,

    /// <summary>It took longer than <see cref="SafetyTimeouts.PendingUpdateSearch"/>.</summary>
    TimedOut,

    /// <summary>Windows Update would not answer.</summary>
    Failed,
}
