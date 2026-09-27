#if DEBUG
namespace PCManager.Core.Monitoring.Demo;

/// <summary>DEBUG-only fake <see cref="IDriveMonitor"/> used when
/// <see cref="DemoDataMode.IsEnabled"/> is set - see <see cref="DemoDataMode"/>.</summary>
internal sealed class DemoDriveMonitor : IDriveMonitor
{
    private static readonly IReadOnlyList<DriveSnapshot> Drives =
    [
        new DriveSnapshot(@"C:\", null, "NTFS", 930_000_000_000, 190_000_000_000, IsLow: false),
        new DriveSnapshot(@"D:\", "Data", "NTFS", 2_000_000_000_000, 900_000_000_000, IsLow: false),
        new DriveSnapshot(@"E:\", "Backup", "NTFS", 1_000_000_000_000, 60_000_000_000, IsLow: true),
    ];

    public IReadOnlyList<DriveSnapshot> GetDrives() => Drives;
}
#endif
