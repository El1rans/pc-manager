namespace Porchlight.Core.Winget;

/// <summary>What the last update check found. See <see cref="IPendingUpdatesTracker"/>.</summary>
/// <param name="Count">Updates available (ignored ones excluded).</param>
/// <param name="CheckedAt">When the check finished.</param>
public sealed record PendingUpdatesStatus(int Count, DateTimeOffset CheckedAt);
