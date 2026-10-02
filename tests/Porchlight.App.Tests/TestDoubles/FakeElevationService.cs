using Porchlight.Core.Elevation;

namespace Porchlight.App.Tests.TestDoubles;

/// <summary>Shared <see cref="IElevationService"/> fake: set <see cref="IsElevated"/> via the
/// constructor or the property; <see cref="RestartElevated"/> returns <see cref="RestartResult"/>.</summary>
internal sealed class FakeElevationService(bool isElevated = false) : IElevationService
{
    public bool IsElevated { get; set; } = isElevated;

    public bool RestartResult { get; set; } = true;

    public bool RestartElevated() => RestartResult;
}
