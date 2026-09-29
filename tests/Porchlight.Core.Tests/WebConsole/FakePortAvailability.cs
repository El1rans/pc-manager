using Porchlight.Core.WebConsole;

namespace Porchlight.Core.Tests.WebConsole;

/// <summary><see cref="IPortAvailability"/> where every port is free unless listed in
/// <see cref="BusyPorts"/>.</summary>
internal sealed class FakePortAvailability : IPortAvailability
{
    public HashSet<int> BusyPorts { get; } = [];

    public List<int> Checked { get; } = [];

    public bool IsFree(int port)
    {
        Checked.Add(port);
        return !BusyPorts.Contains(port);
    }
}
