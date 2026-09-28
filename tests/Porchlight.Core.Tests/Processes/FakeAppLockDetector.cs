using Porchlight.Core.Processes;

namespace Porchlight.Core.Tests.Processes;

internal sealed class FakeAppLockDetector : IAppLockDetector
{
    public IReadOnlyList<string> Names { get; set; } = [];

    public List<string> Calls { get; } = [];

    public IReadOnlyList<string> FindLockingProcessNames(string installLocation)
    {
        Calls.Add(installLocation);
        return Names;
    }
}
