namespace Porchlight.Core.Winget;

/// <inheritdoc cref="IPendingUpdatesTracker"/>
public sealed class PendingUpdatesTracker : IPendingUpdatesTracker
{
    private volatile PendingUpdatesStatus? _current;

    public PendingUpdatesStatus? Current => _current;

    public void Report(int count, DateTimeOffset checkedAt) =>
        _current = new PendingUpdatesStatus(Math.Max(count, 0), checkedAt);

    public void Report(IReadOnlyList<PendingUpdate> updates, DateTimeOffset checkedAt) =>
        _current = new PendingUpdatesStatus(updates.Count, checkedAt, updates);
}
