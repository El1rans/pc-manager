using PCManager.Core.Hardware;

namespace PCManager.Core.Tests.Hardware;

public sealed class FakeFanControlActivityMarker : IFanControlActivityMarker
{
    public bool MarkerExists { get; set; }

    public int CreateCallCount { get; private set; }

    public int DeleteCallCount { get; private set; }

    public bool Exists() => MarkerExists;

    public void Create()
    {
        CreateCallCount++;
        MarkerExists = true;
    }

    public void Delete()
    {
        DeleteCallCount++;
        MarkerExists = false;
    }
}
