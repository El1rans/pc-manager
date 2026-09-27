using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace PCManager.App.Features.RemoteSupport;

/// <inheritdoc cref="IClipboardService"/>
public sealed partial class ClipboardService : IClipboardService
{
    private readonly ILogger<ClipboardService> _logger;

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger;
    }

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (ExternalException ex)
        {
            // Another process can briefly hold the clipboard open; not worth surfacing to the user
            // as an error for a "Copy address" button - they can just click it again.
            LogCouldNotSetClipboard(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not set clipboard text.")]
    private partial void LogCouldNotSetClipboard(Exception exception);
}
