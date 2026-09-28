using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.App.Features.RemoteSupport;

/// <inheritdoc cref="IClipboardService"/>
public sealed partial class ClipboardService : IClipboardService
{
    private readonly ILogger<ClipboardService> _logger;

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger;
    }

    public bool SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch (ExternalException ex)
        {
            // Another process can briefly hold the clipboard open; the caller shows the user a
            // "couldn't copy, try again" message rather than a false "Copied" confirmation.
            LogCouldNotSetClipboard(ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not set clipboard text.")]
    private partial void LogCouldNotSetClipboard(Exception exception);
}
