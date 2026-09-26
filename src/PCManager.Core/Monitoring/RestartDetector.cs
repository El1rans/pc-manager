using Microsoft.Win32;

namespace PCManager.Core.Monitoring;

/// <inheritdoc cref="IRestartDetector"/>
public sealed class RestartDetector : IRestartDetector
{
    private const string ComponentBasedServicingKey =
        @"SYSTEM\CurrentControlSet\Control\Session Manager\Component Based Servicing\RebootPending";

    private const string WindowsUpdateKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired";

    public bool IsRestartPending() =>
        KeyExists(ComponentBasedServicingKey) || KeyExists(WindowsUpdateKey);

    private static bool KeyExists(string subKey)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKey);
        return key is not null;
    }
}
