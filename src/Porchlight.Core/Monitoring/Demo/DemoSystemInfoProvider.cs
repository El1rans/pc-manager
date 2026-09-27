#if DEBUG
namespace Porchlight.Core.Monitoring.Demo;

/// <summary>DEBUG-only fake <see cref="ISystemInfoProvider"/> used when
/// <see cref="DemoDataMode.IsEnabled"/> is set - see <see cref="DemoDataMode"/>.</summary>
internal sealed class DemoSystemInfoProvider : ISystemInfoProvider
{
    public Task<SystemInfo> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SystemInfo(
            ComputerName: "DEMO-PC",
            OsCaption: "Microsoft Windows 11 Pro",
            OsBuild: "26200",
            Manufacturer: "Contoso",
            Model: "Demo Desktop 3000",
            CpuName: "Demo CPU X8 5800",
            PhysicalCores: 8,
            LogicalProcessors: 16,
            GpuNames: ["Demo Graphics 4070"],
            TotalRamBytes: 64L * 1024 * 1024 * 1024,
            LastBootTimeUtc: DateTime.UtcNow.AddDays(-3)));
}
#endif
