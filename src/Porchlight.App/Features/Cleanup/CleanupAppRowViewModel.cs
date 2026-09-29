using Porchlight.Core.Cleanup;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Cleanup;

/// <summary>One installed app on the "Large apps" card.</summary>
public sealed class CleanupAppRowViewModel
{
    public CleanupAppRowViewModel(InstalledApp app, bool canUninstall, DateOnly today)
    {
        App = app;
        CanUninstall = canUninstall;
        SizeText = app.EstimatedSizeBytes is { } bytes ? ByteFormatter.FormatBytes(bytes) : "Size unknown";
        InstalledText = CleanupTextFormatter.FormatInstalled(app.InstallDate, today);
    }

    public InstalledApp App { get; }

    public string Name => App.DisplayName;

    public string Publisher => App.Publisher ?? string.Empty;

    public string SizeText { get; }

    public string InstalledText { get; }

    /// <summary>False while Porchlight is elevated and this is a per-user entry: the row shows
    /// "Open Installed apps" instead of "Uninstall...".</summary>
    public bool CanUninstall { get; }

    public bool CannotUninstall => !CanUninstall;
}
