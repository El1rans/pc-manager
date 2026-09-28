using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Porchlight.Core.Monitoring;

/// <inheritdoc cref="IRestartDetector"/>
public sealed partial class RestartDetector(ILogger<RestartDetector> logger) : IRestartDetector
{
    private const string ComponentBasedServicingKey =
        @"SYSTEM\CurrentControlSet\Control\Session Manager\Component Based Servicing\RebootPending";

    private const string WindowsUpdateKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired";

    public bool IsRestartPending()
    {
        try
        {
            return KeyExists(ComponentBasedServicingKey) || KeyExists(WindowsUpdateKey);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            LogRestartCheckFailed(ex);
            return false;
        }
    }

    private static bool KeyExists(string subKey)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKey);
        return key is not null;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to check pending-restart registry keys.")]
    private partial void LogRestartCheckFailed(Exception ex);
}
