using System.Management;
using Microsoft.Extensions.Logging;

namespace PCManager.Core.Monitoring;

/// <inheritdoc cref="ISystemInfoProvider"/>
public sealed class SystemInfoProvider(ILogger<SystemInfoProvider> logger) : ISystemInfoProvider
{
    private const string Unknown = "Unknown";

    public Task<SystemInfo> GetAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Build(cancellationToken), cancellationToken);

    private SystemInfo Build(CancellationToken cancellationToken)
    {
        var (osCaption, osBuild, lastBoot) = QueryOperatingSystem(cancellationToken);
        var (computerName, manufacturer, model, totalRam) = QueryComputerSystem(cancellationToken);
        var (cpuName, physicalCores, logicalProcessors) = QueryProcessor(cancellationToken);
        var gpuNames = QueryVideoControllers(cancellationToken);

        return new SystemInfo(
            computerName,
            osCaption,
            osBuild,
            manufacturer,
            model,
            cpuName,
            physicalCores,
            logicalProcessors,
            gpuNames,
            totalRam,
            lastBoot);
    }

    private (string Caption, string Build, DateTime? LastBootUtc) QueryOperatingSystem(CancellationToken cancellationToken)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption, BuildNumber, LastBootUpTime FROM Win32_OperatingSystem");
            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var caption = (item["Caption"] as string)?.Trim() ?? Unknown;
                    var build = item["BuildNumber"] as string ?? Unknown;
                    DateTime? lastBootUtc = null;
                    if (item["LastBootUpTime"] is string wmiDate)
                    {
                        lastBootUtc = ManagementDateTimeConverter.ToDateTime(wmiDate).ToUniversalTime();
                    }

                    return (caption, build, lastBootUtc);
                }
            }
        }
        catch (ManagementException ex)
        {
            logger.LogDebug(ex, "Failed to query Win32_OperatingSystem.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogDebug(ex, "Access denied querying Win32_OperatingSystem.");
        }

        return (Unknown, Unknown, null);
    }

    private (string ComputerName, string Manufacturer, string Model, long TotalRamBytes) QueryComputerSystem(CancellationToken cancellationToken)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem");
            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = item["Name"] as string ?? Unknown;
                    var manufacturer = item["Manufacturer"] as string ?? Unknown;
                    var model = item["Model"] as string ?? Unknown;
                    var totalRam = item["TotalPhysicalMemory"] is ulong ram ? (long)ram : 0;
                    return (name, manufacturer, model, totalRam);
                }
            }
        }
        catch (ManagementException ex)
        {
            logger.LogDebug(ex, "Failed to query Win32_ComputerSystem.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogDebug(ex, "Access denied querying Win32_ComputerSystem.");
        }

        return (Unknown, Unknown, Unknown, 0);
    }

    private (string CpuName, int PhysicalCores, int LogicalProcessors) QueryProcessor(CancellationToken cancellationToken)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            using var results = searcher.Get();
            string? cpuName = null;
            var physicalCores = 0;
            var logicalProcessors = 0;
            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    cpuName ??= (item["Name"] as string)?.Trim();
                    physicalCores += item["NumberOfCores"] is uint cores ? (int)cores : 0;
                    logicalProcessors += item["NumberOfLogicalProcessors"] is uint logical ? (int)logical : 0;
                }
            }

            if (cpuName is not null)
            {
                return (cpuName, physicalCores, logicalProcessors);
            }
        }
        catch (ManagementException ex)
        {
            logger.LogDebug(ex, "Failed to query Win32_Processor.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogDebug(ex, "Access denied querying Win32_Processor.");
        }

        return (Unknown, 0, 0);
    }

    private List<string> QueryVideoControllers(CancellationToken cancellationToken)
    {
        var names = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (item["Name"] is string name && name.Length > 0)
                    {
                        names.Add(name);
                    }
                }
            }
        }
        catch (ManagementException ex)
        {
            logger.LogDebug(ex, "Failed to query Win32_VideoController.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogDebug(ex, "Access denied querying Win32_VideoController.");
        }

        return names;
    }
}
