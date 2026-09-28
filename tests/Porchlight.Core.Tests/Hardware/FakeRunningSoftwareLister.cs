using Porchlight.Core.Hardware;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>In-memory <see cref="IRunningSoftwareLister"/> fake so
/// <see cref="FanControlConflictDetector"/> can be unit tested without touching real processes or
/// Windows services.</summary>
internal sealed class FakeRunningSoftwareLister : IRunningSoftwareLister
{
    public List<string> RunningProcessNames { get; } = [];

    public List<string> RunningServiceNames { get; } = [];

    public IReadOnlyCollection<string> GetRunningProcessNames() => RunningProcessNames;

    public IReadOnlyCollection<string> GetRunningServiceNames() => RunningServiceNames;
}
