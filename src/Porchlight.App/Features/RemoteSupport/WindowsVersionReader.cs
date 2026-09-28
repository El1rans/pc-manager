using Microsoft.Win32;

namespace Porchlight.App.Features.RemoteSupport;

/// <inheritdoc cref="IWindowsVersionReader"/>
public sealed class WindowsVersionReader : IWindowsVersionReader
{
    // Microsoft.Win32.Registry (not the shared Core IRegistryReader, which only covers the
    // uninstall/service keys IComponentService needs) - a plain, read-only registry read local to
    // this one support-info string.
    private const string CurrentVersionKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    // ProductName still says "Windows 10" on Windows 11 (Microsoft never updated it); the build
    // number is the documented way to tell them apart.
    private const int Windows11MinimumBuildNumber = 22000;

    public string GetFriendlyVersion()
    {
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKeyPath);
        if (key is null)
        {
            return "Windows (version unknown)";
        }

        var productName = key.GetValue("ProductName") as string ?? "Windows";
        var displayVersion = key.GetValue("DisplayVersion") as string;
        var buildText = key.GetValue("CurrentBuild") as string;

        if (productName.Contains("Windows 10", StringComparison.Ordinal) &&
            int.TryParse(buildText, out var build) && build >= Windows11MinimumBuildNumber)
        {
            productName = productName.Replace("Windows 10", "Windows 11", StringComparison.Ordinal);
        }

        var suffix = displayVersion is { Length: > 0 } ? $" {displayVersion}" : string.Empty;
        var buildSuffix = buildText is { Length: > 0 } ? $" (build {buildText})" : string.Empty;
        return $"{productName}{suffix}{buildSuffix}";
    }
}
