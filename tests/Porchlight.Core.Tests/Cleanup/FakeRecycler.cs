using Porchlight.Core.Cleanup;

namespace Porchlight.Core.Tests.Cleanup;

/// <summary>Records what was sent to the "Recycle Bin"; paths in <see cref="Refuse"/> fail.</summary>
internal sealed class FakeRecycler : IRecycler
{
    public List<string> Recycled { get; } = [];

    public HashSet<string> Refuse { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool MoveToRecycleBin(string path)
    {
        if (Refuse.Contains(path))
        {
            return false;
        }

        Recycled.Add(path);
        return true;
    }
}
