using Microsoft.Win32;

namespace PCManager.Core.Components;

/// <inheritdoc cref="IRegistryReader"/>
public sealed class RegistryReader : IRegistryReader
{
    private static readonly (RegistryKey Hive, string SubKey)[] UninstallRoots =
    [
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    public UninstallEntry? FindUninstallEntry(string displayNameContains)
    {
        ArgumentException.ThrowIfNullOrEmpty(displayNameContains);

        foreach (var (hive, subKey) in UninstallRoots)
        {
            using var uninstallKey = hive.OpenSubKey(subKey);
            if (uninstallKey is null)
            {
                continue;
            }

            foreach (var name in uninstallKey.GetSubKeyNames())
            {
                using var entryKey = uninstallKey.OpenSubKey(name);
                var displayName = entryKey?.GetValue("DisplayName") as string;
                if (displayName is null ||
                    !displayName.Contains(displayNameContains, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new UninstallEntry(
                    entryKey!.GetValue("DisplayVersion") as string,
                    entryKey.GetValue("InstallLocation") as string);
            }
        }

        return null;
    }

    public bool ServiceExists(string serviceName)
    {
        ArgumentException.ThrowIfNullOrEmpty(serviceName);

        using var servicesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        using var serviceKey = servicesKey?.OpenSubKey(serviceName);
        return serviceKey is not null;
    }
}
