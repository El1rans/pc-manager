using PCManager.Core.Elevation;

namespace PCManager.Core.Tests.Components;

internal sealed class FakeElevationService : IElevationService
{
    public bool IsElevated { get; set; }

    public bool RestartElevated() => true;
}
