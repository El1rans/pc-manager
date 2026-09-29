#if DEBUG
namespace Porchlight.Core.Cleanup.Demo;

/// <summary>DEBUG-only fake <see cref="IInstalledAppsReader"/> returning made-up apps - see
/// <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoInstalledAppsReader : IInstalledAppsReader
{
    public IReadOnlyList<InstalledApp> GetInstalledApps() =>
    [
        new("Demo Photo Studio", "Example Software", "4.2", 4_800L * 1024 * 1024, new DateOnly(2021, 3, 14), "demo.exe", true),
        new("Sample Video Editor", "Sample Co", "12.0", 2_100L * 1024 * 1024, new DateOnly(2023, 8, 2), "demo.exe", true),
        new("Example Chat", "Example Inc", "1.9", 350L * 1024 * 1024, new DateOnly(2024, 1, 20), "demo.exe", false),
        new("Tiny Utility", null, null, null, null, "demo.exe", true),
    ];
}
#endif
