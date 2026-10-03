using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Cleanup;
using Porchlight.Core.RunningApps;

namespace Porchlight.Core.Safety;

/// <inheritdoc cref="IRemoteToolProbe"/>
public sealed partial class RemoteToolProbe(
    IProcessSnapshotSource processes,
    IInstalledAppsReader installedApps,
    ILogger<RemoteToolProbe> logger) : IRemoteToolProbe
{
    public Task<RemoteToolEvidence> CollectAsync(CancellationToken cancellationToken) =>
        Task.Run(Collect, cancellationToken);

    private RemoteToolEvidence Collect()
    {
        var processNames = Safe(() => processes.Capture().Select(p => p.Name).ToList(), "running programs");
        var appNames = Safe(() => installedApps.GetInstalledApps().Select(a => a.DisplayName).ToList(), "installed apps");
        var serviceNames = Safe(ReadServiceNames, "services");
        return new RemoteToolEvidence(processNames, appNames, serviceNames);
    }

    private static List<string> ReadServiceNames()
    {
        var names = new List<string>();
        foreach (var service in ServiceController.GetServices())
        {
            using (service)
            {
                names.Add(service.ServiceName);
                names.Add(service.DisplayName);
            }
        }

        return names;
    }

    private List<string> Safe(Func<List<string>> read, string what)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception
            or UnauthorizedAccessException or IOException)
        {
            LogSourceFailed(ex, what);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read {What} while looking for remote-control tools.")]
    private partial void LogSourceFailed(Exception ex, string what);
}
