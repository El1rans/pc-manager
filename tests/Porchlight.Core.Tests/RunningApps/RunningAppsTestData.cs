using Porchlight.Core.RunningApps;

namespace Porchlight.Core.Tests.RunningApps;

internal static class RunningAppsTestData
{
    public static readonly DateTime Started = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    public static ProcessSample Sample(
        int pid,
        string name = "app",
        string? path = @"C:\Program Files\App\app.exe",
        int session = 1,
        bool window = false,
        double cpuSeconds = 0,
        long memory = 1000,
        DateTime? start = null) =>
        new(pid, name, path, session, window, start ?? Started, TimeSpan.FromSeconds(cpuSeconds), memory);
}

internal sealed class FakeProcessSnapshotSource : IProcessSnapshotSource
{
    public List<ProcessSample> Samples { get; set; } = [];

    public IReadOnlyList<ProcessSample> Capture() => [.. Samples];
}

internal sealed class FakeProcessKiller : IProcessKiller
{
    public Dictionary<int, ProcessKillOutcome> Outcomes { get; } = [];

    public List<(int Pid, DateTime? Start)> Calls { get; } = [];

    public ProcessKillOutcome Kill(int pid, DateTime? expectedStartTime)
    {
        Calls.Add((pid, expectedStartTime));
        return Outcomes.GetValueOrDefault(pid, ProcessKillOutcome.Killed);
    }
}

internal sealed class FakeSystemMemoryInfo : ISystemMemoryInfo
{
    public MemoryStatus? Status { get; set; } = new(16_000, 6_000);

    public MemoryStatus? Read() => Status;
}
