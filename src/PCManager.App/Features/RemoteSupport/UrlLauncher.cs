using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PCManager.App.Features.RemoteSupport;

/// <inheritdoc cref="IUrlLauncher"/>
public sealed partial class UrlLauncher : IUrlLauncher
{
    private readonly ILogger<UrlLauncher> _logger;

    public UrlLauncher(ILogger<UrlLauncher> logger)
    {
        _logger = logger;
    }

    public void Open(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        try
        {
            // UseShellExecute: true hands the URL to Windows' own "open with the default browser"
            // handling, rather than trying to run it as an executable (ProcessRunner/StartDetached
            // use UseShellExecute: false, which is not suited to opening a URL).
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not open {Url} in the default browser.", url);
        }
    }
}
