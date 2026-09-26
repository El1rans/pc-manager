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
    private readonly bool _isElevated;

    public ElevationService(ILogger<ElevationService> logger)
    {
        _logger = logger;
        _isElevated = ComputeIsElevated();
    }

    /// <summary>
    /// Elevation cannot change for the lifetime of a running process, so it is computed once at
    /// construction rather than on every access.
    /// </summary>
    public bool IsElevated => _isElevated;

    private static bool ComputeIsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
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

        // Forward this process's own command-line arguments so the elevated relaunch starts with
        // the same arguments (e.g. anything the host or a future updater passes on launch).
        foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
        {
            startInfo.ArgumentList.Add(arg);
        }

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
