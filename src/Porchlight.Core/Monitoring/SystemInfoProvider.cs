using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Monitoring;

/// <inheritdoc cref="ISystemInfoProvider"/>
public sealed partial class SystemInfoProvider(ILogger<SystemInfoProvider> logger) : ISystemInfoProvider
{
    private const string Unknown = "Unknown";

    /// <summary>Caps how long a single WMI query can block. A disabled/unreachable WMI service
    /// (COMException 0x80070422, or an RPC server unavailable) would otherwise hang indefinitely
    /// rather than falling back to "Unknown".</summary>
    private static readonly TimeSpan WmiQueryTimeout = TimeSpan.FromSeconds(5);

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
            using var searcher = CreateSearcher("SELECT Caption, BuildNumber, LastBootUpTime FROM Win32_OperatingSystem");
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
        catch (Exception ex) when (IsExpectedWmiFailure(ex))
        {
            LogWmiQueryFailed(ex, "Win32_OperatingSystem");
        }

        return (Unknown, Unknown, null);
    }

    private (string ComputerName, string Manufacturer, string Model, long TotalRamBytes) QueryComputerSystem(CancellationToken cancellationToken)
    {
        try
        {
            using var searcher = CreateSearcher("SELECT Name, Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem");
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
        catch (Exception ex) when (IsExpectedWmiFailure(ex))
        {
            LogWmiQueryFailed(ex, "Win32_ComputerSystem");
        }

        return (Unknown, Unknown, Unknown, 0);
    }

    private (string CpuName, int PhysicalCores, int LogicalProcessors) QueryProcessor(CancellationToken cancellationToken)
    {
        try
        {
            using var searcher = CreateSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
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
        catch (Exception ex) when (IsExpectedWmiFailure(ex))
        {
            LogWmiQueryFailed(ex, "Win32_Processor");
        }

        return (Unknown, 0, 0);
    }

    private List<string> QueryVideoControllers(CancellationToken cancellationToken)
    {
        var names = new List<string>();
        try
        {
            using var searcher = CreateSearcher("SELECT Name FROM Win32_VideoController");
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
        catch (Exception ex) when (IsExpectedWmiFailure(ex))
        {
            LogWmiQueryFailed(ex, "Win32_VideoController");
        }

        return names;
    }

    private static ManagementObjectSearcher CreateSearcher(string query) =>
        new(query) { Options = new System.Management.EnumerationOptions { Timeout = WmiQueryTimeout } };

    /// <summary>
    /// Every way a WMI query can fail on a real machine: the class/provider itself
    /// (<see cref="ManagementException"/>), permissions (<see cref="UnauthorizedAccessException"/>),
    /// WMI disabled or unreachable - e.g. 0x80070422, or the RPC server unavailable
    /// (<see cref="COMException"/>) - and a malformed date from
    /// <see cref="ManagementDateTimeConverter.ToDateTime(string)"/>
    /// (<see cref="FormatException"/>, <see cref="ArgumentOutOfRangeException"/>).
    /// </summary>
    private static bool IsExpectedWmiFailure(Exception ex) =>
        ex is ManagementException or UnauthorizedAccessException or COMException
            or FormatException or ArgumentOutOfRangeException;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to query {WmiClass}; leaving its fields Unknown.")]
    private partial void LogWmiQueryFailed(Exception ex, string wmiClass);
}
