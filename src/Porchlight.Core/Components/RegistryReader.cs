using Microsoft.Win32;

namespace Porchlight.Core.Components;

/// <inheritdoc cref="IRegistryReader"/>
public sealed class RegistryReader : IRegistryReader
{
    private const string InstallerKeyPath = @"Software\Porchlight\Installer";

    // Pre-rebrand installers (before docs/specs/08-rebrand-porchlight.md) wrote this key instead.
    // Read as a fallback only - a Porchlight installer never writes here - so an install that
    // upgraded over an old "PC Manager" install without a repair/reinstall still gets the
    // "already installer-handled" hint for first-run setup.
    private const string LegacyInstallerKeyPath = @"Software\PC Manager\Installer";

    private const string InstallerComponentsValue = "Components";

    private static readonly (RegistryKey Hive, string SubKey, bool IsPerMachine)[] UninstallRoots =
    [
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", true),
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false),
    ];

    public UninstallEntry? FindUninstallEntry(string displayNameContains)
    {
        ArgumentException.ThrowIfNullOrEmpty(displayNameContains);

        foreach (var (hive, subKey, isPerMachine) in UninstallRoots)
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
                    entryKey.GetValue("InstallLocation") as string,
                    isPerMachine);
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

    public IReadOnlyList<string> GetInstallerHandledComponentIds()
    {
        var raw = ReadInstallerComponentsValue(InstallerKeyPath) ?? ReadInstallerComponentsValue(LegacyInstallerKeyPath);
        if (raw is not { Length: > 0 })
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string? ReadInstallerComponentsValue(string keyPath)
    {
        using var installerKey = Registry.LocalMachine.OpenSubKey(keyPath);
        return installerKey?.GetValue(InstallerComponentsValue) as string;
    }
}
