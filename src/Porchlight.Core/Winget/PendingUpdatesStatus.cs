namespace Porchlight.Core.Winget;

/// <summary>What the last update check found. See <see cref="IPendingUpdatesTracker"/>.</summary>
/// <param name="Count">Updates available (ignored ones excluded).</param>
/// <param name="CheckedAt">When the check finished.</param>
/// <param name="Updates">The updates themselves, when the reporter listed them (null when only a count was reported).</param>
public sealed record PendingUpdatesStatus(int Count, DateTimeOffset CheckedAt, IReadOnlyList<PendingUpdate>? Updates = null)
{
    /// <summary>The waiting updates, never null.</summary>
    public IReadOnlyList<PendingUpdate> Items => Updates ?? [];
}
