namespace Porchlight.Core.Winget;

/// <summary>
/// Remembers the result of the most recent app-update check so other features (the check-up report)
/// can say how many updates are waiting without starting another <c>winget</c> run. The Updates page
/// reports to it after every check.
/// </summary>
public interface IPendingUpdatesTracker
{
    /// <summary>The last reported check, or null if no check has finished since Porchlight started.</summary>
    PendingUpdatesStatus? Current { get; }

    /// <summary>Records the outcome of a finished check.</summary>
    /// <param name="count">Number of updates available, not counting ones the user chose to ignore.</param>
    /// <param name="checkedAt">When the check finished.</param>
    void Report(int count, DateTimeOffset checkedAt);

    /// <summary>Records the outcome of a finished check together with the updates found, so the web
    /// console can list them without running <c>winget</c> itself.</summary>
    /// <param name="updates">The updates available, not counting ones the user chose to ignore.</param>
    /// <param name="checkedAt">When the check finished.</param>
    void Report(IReadOnlyList<PendingUpdate> updates, DateTimeOffset checkedAt);
}
