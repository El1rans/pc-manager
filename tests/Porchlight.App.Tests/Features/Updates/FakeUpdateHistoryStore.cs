using Porchlight.Core.Winget;

namespace Porchlight.App.Tests.Features.Updates;

/// <summary>In-memory <see cref="IUpdateHistoryStore"/> that records what was added.</summary>
internal sealed class FakeUpdateHistoryStore : IUpdateHistoryStore
{
    private readonly List<UpdateHistoryEntry> _entries = [];

    public bool ThrowOnAdd { get; set; }

    public IReadOnlyList<UpdateHistoryEntry> GetAll() => [.. _entries];

    public void Add(UpdateHistoryEntry entry)
    {
        if (ThrowOnAdd)
        {
            throw new InvalidOperationException("boom");
        }

        _entries.Add(entry);
    }

    public void Clear() => _entries.Clear();
}
