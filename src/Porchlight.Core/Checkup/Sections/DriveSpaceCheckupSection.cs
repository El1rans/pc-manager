using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Checkup.Sections;

/// <summary>Free space on each drive. Shows drive letters only - never volume labels.</summary>
public sealed class DriveSpaceCheckupSection : ICheckupSection
{
    /// <summary>Free space below which a drive is a real problem, not just low.</summary>
    public const long CriticalFreeBytes = 5L * 1024 * 1024 * 1024;

    private readonly IDriveMonitor _driveMonitor;

    public DriveSpaceCheckupSection(IDriveMonitor driveMonitor)
    {
        _driveMonitor = driveMonitor;
    }

    public string Title => "Drive space";

    public int Order => 300;

    public Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken)
    {
        var drives = _driveMonitor.GetDrives();
        if (drives.Count == 0)
        {
            return Task.FromResult<CheckupSectionResult?>(null);
        }

        var severity = CheckupSeverity.Ok;
        List<string> lines = [];
        foreach (var drive in drives)
        {
            var name = drive.Name.TrimEnd('\\');
            var text = $"{name} {ByteFormatter.FormatBytes(drive.FreeBytes)} free of {ByteFormatter.FormatBytes(drive.TotalBytes)}";
            if (drive.FreeBytes < CriticalFreeBytes && drive.TotalBytes > 0)
            {
                severity = CheckupSeverity.Problem;
                text += " - almost full";
            }
            else if (drive.IsLow)
            {
                severity = severity < CheckupSeverity.NeedsAttention ? CheckupSeverity.NeedsAttention : severity;
                text += " - getting full";
            }

            lines.Add(text);
        }

        return Task.FromResult<CheckupSectionResult?>(new CheckupSectionResult(Title, severity, lines));
    }
}
