using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace PCManager.Core.Elevation;

/// <inheritdoc cref="IElevationService"/>
public sealed class ElevationService : IElevationService
{
    /// <summary>Win32 error code for "the operation was canceled by the user" (UAC decline).</summary>
    private const int ErrorCancelled = 1223;

    private readonly ILogger<ElevationService> _logger;

    public ElevationService(ILogger<ElevationService> logger)
    {
        _logger = logger;
    }

    public bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public bool RestartElevated()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            _logger.LogWarning("Could not determine the current process path; cannot restart elevated.");
            return false;
        }

        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = true,
            Verb = "runas",
        };

        try
        {
            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            // The user declined the UAC prompt. Not an error; stay running as-is.
            _logger.LogDebug(ex, "User declined the elevation prompt.");
            return false;
        }
    }
}
