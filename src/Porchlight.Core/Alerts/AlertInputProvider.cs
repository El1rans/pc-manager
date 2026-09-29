using System.IO;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Hardware;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Alerts;

/// <inheritdoc cref="IAlertInputProvider"/>
public sealed class AlertInputProvider(
    IDriveMonitor driveMonitor,
    IHardwareService hardwareService,
    IRestartDetector restartDetector,
    TimeProvider timeProvider,
    ILogger<AlertInputProvider> logger) : IAlertInputProvider
{
    /// <summary>A hardware snapshot older than this (service stopped or stuck) is ignored rather
    /// than trusted as a current temperature.</summary>
    private static readonly TimeSpan MaxSnapshotAge = TimeSpan.FromSeconds(30);

    public AlertInputs GetInputs()
    {
        var (cpu, gpu) = ReadTemperatures();
        return new AlertInputs(ReadFixedDrives(), cpu, gpu, ReadRestartPending());
    }

    private List<DriveSnapshot> ReadFixedDrives()
    {
        try
        {
            return [.. driveMonitor.GetDrives().Where(IsFixedDrive)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogDebug(ex, "Could not read drives for alerts; skipping the disk check this round.");
            return [];
        }
    }

    private bool IsFixedDrive(DriveSnapshot drive)
    {
        try
        {
            return new DriveInfo(drive.Name).DriveType == DriveType.Fixed;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not tell whether a drive is fixed; ignoring it for alerts.");
            return false;
        }
    }

    private (double? Cpu, double? Gpu) ReadTemperatures()
    {
        try
        {
            var snapshot = hardwareService.Latest;
            if (snapshot.Status is not (HardwareStatus.Ready or HardwareStatus.NotElevated)
                || timeProvider.GetUtcNow() - snapshot.TimestampUtc > MaxSnapshotAge)
            {
                return (null, null);
            }

            return (
                Find(snapshot, HardwareNodeType.Cpu, HardwareSummarySelector.SelectCpuTemperature),
                Find(snapshot, HardwareNodeType.Gpu, HardwareSummarySelector.SelectGpuTemperature));
        }
        catch (Exception ex)
        {
            // The hardware layer is third-party-backed; any failure just means "no reading".
            logger.LogDebug(ex, "Could not read temperatures for alerts; treating them as unavailable.");
            return (null, null);
        }
    }

    private static double? Find(HardwareSnapshot snapshot, HardwareNodeType type, Func<HardwareNode, SensorReading?> select)
    {
        var node = Flatten(snapshot.Nodes).FirstOrDefault(n => n.Type == type);
        return node is null ? null : select(node)?.Value;
    }

    private static IEnumerable<HardwareNode> Flatten(IReadOnlyList<HardwareNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    private bool ReadRestartPending()
    {
        try
        {
            return restartDetector.IsRestartPending();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not check for a pending restart; assuming none.");
            return false;
        }
    }
}
