using PCManager.Core.Components;

namespace PCManager.Core.Tests.Components;

internal sealed class FakeProcessProbe : IProcessProbe
{
    private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);

    public void SetRunning(string processName) => _running.Add(processName);

    public bool IsRunning(string processName) => _running.Contains(processName);
}
