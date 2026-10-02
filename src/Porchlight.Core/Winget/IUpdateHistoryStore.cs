namespace Porchlight.Core.Winget;

/// <summary>The local log of updates, reinstalls and installs Porchlight ran through winget.</summary>
public interface IUpdateHistoryStore
{
    /// <summary>A snapshot of every entry, oldest first.</summary>
    IReadOnlyList<UpdateHistoryEntry> GetAll();

    void Add(UpdateHistoryEntry entry);

    void Clear();
}
