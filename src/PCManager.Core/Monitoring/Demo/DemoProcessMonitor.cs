#if DEBUG
namespace PCManager.Core.Monitoring.Demo;

/// <summary>DEBUG-only fake <see cref="IProcessMonitor"/> used when
/// <see cref="DemoDataMode.IsEnabled"/> is set - see <see cref="DemoDataMode"/>.</summary>
internal sealed class DemoProcessMonitor : IProcessMonitor
{
    private static readonly IReadOnlyList<ProcessGroupSnapshot> Processes =
    [
        new ProcessGroupSnapshot("Demo Browser", 6, 3.4, 57_100_000),
        new ProcessGroupSnapshot("Demo Chat App", 2, 2.9, 320_000_000),
        new ProcessGroupSnapshot("Demo Media Player", 1, 0.8, 154_400_000),
        new ProcessGroupSnapshot("Demo Game Launcher", 1, 0.4, 318_700_000),
        new ProcessGroupSnapshot("Demo Cloud Sync", 1, 0.1, 263_900_000),
        new ProcessGroupSnapshot("PCManager", 1, 0.1, 207_400_000),
        new ProcessGroupSnapshot("Demo Background Service", 1, 0.1, 22_500_000),
        new ProcessGroupSnapshot("Demo Utility", 1, 0.1, 15_600_000),
    ];

    public IReadOnlyList<ProcessGroupSnapshot> SampleTop(int count) =>
        Processes.Take(count).ToList();
}
#endif
