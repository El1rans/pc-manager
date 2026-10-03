using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="IInstalledAppsReader"/>
public sealed partial class InstalledAppsReader : IInstalledAppsReader
{
    private static readonly (RegistryKey Hive, string SubKey, bool IsPerMachine)[] UninstallRoots =
    [
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", true),
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false),
    ];

    private readonly ILogger<InstalledAppsReader> _logger;

    public InstalledAppsReader(ILogger<InstalledAppsReader> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<InstalledApp> GetInstalledApps() => Read(includeProtectedNames: false);

    public IReadOnlyList<InstalledApp> GetAllInstalledApps() => Read(includeProtectedNames: true);

    private List<InstalledApp> Read(bool includeProtectedNames)
    {
        var raw = new List<RawUninstallEntry>();
        foreach (var (hive, subKey, isPerMachine) in UninstallRoots)
        {
            ReadHive(hive, subKey, isPerMachine, raw);
        }

        return [.. InstalledAppFilter.Apply(raw, includeProtectedNames)];
    }

    private void ReadHive(RegistryKey hive, string subKey, bool isPerMachine, List<RawUninstallEntry> into)
    {
        try
        {
            using var uninstallKey = hive.OpenSubKey(subKey);
            if (uninstallKey is null)
            {
                return;
            }

            foreach (var name in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var entryKey = uninstallKey.OpenSubKey(name);
                    if (entryKey is not null)
                    {
                        into.Add(ReadEntry(entryKey, isPerMachine));
                    }
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                {
                    // One unreadable entry must not hide the rest of the list.
                    LogEntryUnreadable(ex, name);
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            LogHiveUnreadable(ex, subKey);
        }
    }

    private static RawUninstallEntry ReadEntry(RegistryKey key, bool isPerMachine) => new(
        key.GetValue("DisplayName") as string,
        key.GetValue("Publisher") as string,
        key.GetValue("DisplayVersion") as string,
        key.GetValue("EstimatedSize") as int?,
        key.GetValue("InstallDate") as string,
        key.GetValue("UninstallString") as string,
        key.GetValue("SystemComponent") as int?,
        key.GetValue("ParentKeyName") as string,
        key.GetValue("ReleaseType") as string,
        isPerMachine);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read uninstall entry {Name}; leaving it out.")]
    private partial void LogEntryUnreadable(Exception ex, string name);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read uninstall key {SubKey}.")]
    private partial void LogHiveUnreadable(Exception ex, string subKey);
}
