using Porchlight.Core.Elevation;

namespace Porchlight.Core.Tests.Components;

internal sealed class FakeElevationService : IElevationService
{
    public bool IsElevated { get; set; }

    public bool RestartElevated() => true;
}
