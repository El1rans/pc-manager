using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Hardware;

/// <inheritdoc cref="IRunningSoftwareLister"/>
/// <remarks>
/// Deliberately thin and read-only, matching <see cref="IRunningSoftwareLister"/>'s contract: this
/// class only enumerates what is already running. Process names come from
/// <see cref="Process.GetProcesses()"/>; service names come from a WMI <c>Win32_Service</c> query
/// (the same technique <c>Monitoring.SystemInfoProvider</c> already uses elsewhere in Core), rather
/// than adding a new <c>System.ServiceProcess.ServiceController</c> package dependency.
/// </remarks>
public sealed partial class RunningSoftwareLister(ILogger<RunningSoftwareLister> logger) : IRunningSoftwareLister
{
    private static readonly TimeSpan WmiQueryTimeout = TimeSpan.FromSeconds(5);

    public IReadOnlyCollection<string> GetRunningProcessNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            LogProcessListFailed(ex);
            return names;
        }

        foreach (var process in processes)
        {
            try
            {
                names.Add(process.ProcessName);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The process may have exited between GetProcesses() and here - not worth logging
                // for every such race, this is best-effort enumeration.
            }
            finally
            {
                process.Dispose();
            }
        }

        return names;
    }

    public IReadOnlyCollection<string> GetRunningServiceNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Service WHERE State = 'Running'")
            {
                Options = new System.Management.EnumerationOptions { Timeout = WmiQueryTimeout },
            };
            using var results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                using (item)
                {
                    if (item["Name"] is string name)
                    {
                        names.Add(name);
                    }
                }
            }
        }
        catch (Exception ex) when (IsExpectedWmiFailure(ex))
        {
            LogServiceListFailed(ex);
        }

        return names;
    }

    private static bool IsExpectedWmiFailure(Exception ex) =>
        ex is ManagementException or UnauthorizedAccessException or COMException;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not enumerate running processes.")]
    private partial void LogProcessListFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not enumerate running Windows services.")]
    private partial void LogServiceListFailed(Exception ex);
}
