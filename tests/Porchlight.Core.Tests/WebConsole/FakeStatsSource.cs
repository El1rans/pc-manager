using Porchlight.Core.Hardware;
using Porchlight.Core.Monitoring;
using Porchlight.Core.WebConsole;

namespace Porchlight.Core.Tests.WebConsole;

/// <summary>Returns one fixed <see cref="WebConsoleStats"/> and counts how often it was asked.</summary>
internal sealed class FakeStatsSource : IWebConsoleStatsSource
{
    public static readonly WebConsoleStats Sample = new(
        new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero),
        new SystemInfo("DEMO-PC", "Windows 11 Pro", "26100", "Contoso", "Desktop", "Demo CPU", 8, 16, ["Demo GPU"], 32L << 30, null),
        new PerformanceSnapshot(12.5, 8L << 30, 32L << 30, 3, 1, 100, 200, 300, 400, 5000, 6000),
        [new DriveSnapshot(@"C:\", "System", "NTFS", 500L << 30, 20L << 30, true)],
        [new ProcessGroupSnapshot("demo", 2, 4.5, 100L << 20)],
        IsRestartPending: true,
        [new HardwareSummaryTile("CPU temperature", "80 °C", "Package", HardwareSummarySeverity.Caution)]);

    public int CallCount { get; private set; }

    public Task<WebConsoleStats> GetAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Sample);
    }
}
