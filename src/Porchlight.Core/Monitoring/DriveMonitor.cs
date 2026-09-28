using System.IO;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Monitoring;

/// <inheritdoc cref="IDriveMonitor"/>
public sealed partial class DriveMonitor(ILogger<DriveMonitor> logger) : IDriveMonitor
{
    public IReadOnlyList<DriveSnapshot> GetDrives()
    {
        var drives = new List<DriveSnapshot>();

        DriveInfo[] allDrives;
        try
        {
            allDrives = DriveInfo.GetDrives();
        }
        catch (IOException ex)
        {
            LogDriveEnumerationFailed(ex);
            return drives;
        }

        foreach (var drive in allDrives)
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
            {
                continue;
            }

            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                var totalBytes = drive.TotalSize;
                var freeBytes = drive.AvailableFreeSpace;
                string? label = null;
                try
                {
                    label = drive.VolumeLabel;
                }
                catch (IOException ex)
                {
                    // Some removable drives report ready but throw reading the label; the drive
                    // still has useful capacity numbers, so keep it with a null label.
                    LogDriveLabelUnavailable(ex, drive.Name);
                }

                drives.Add(new DriveSnapshot(
                    drive.Name,
                    label,
                    drive.DriveFormat,
                    totalBytes,
                    freeBytes,
                    DriveThresholds.IsLow(freeBytes, totalBytes)));
            }
            catch (IOException ex)
            {
                // The drive became unavailable between the IsReady check and reading it (e.g. a
                // removable drive was ejected); skip it rather than fail the whole sample.
                LogDriveUnavailable(ex, drive.Name);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogDriveAccessDenied(ex, drive.Name);
            }
        }

        return drives;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to enumerate drives.")]
    private partial void LogDriveEnumerationFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Drive {Drive} became unavailable while sampling.")]
    private partial void LogDriveUnavailable(Exception ex, string drive);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Access denied reading drive {Drive}.")]
    private partial void LogDriveAccessDenied(Exception ex, string drive);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to read the volume label for drive {Drive}.")]
    private partial void LogDriveLabelUnavailable(Exception ex, string drive);
}
