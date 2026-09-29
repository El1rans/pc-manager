using Porchlight.Core.Cleanup;

namespace Porchlight.Core.Tests.Cleanup;

internal sealed class FakeRecycleBin : IRecycleBin
{
    public RecycleBinInfo Info { get; set; } = new(0, 0);

    public int EmptyCallCount { get; private set; }

    public RecycleBinInfo QuerySize() => Info;

    public bool Empty()
    {
        EmptyCallCount++;
        return true;
    }
}
