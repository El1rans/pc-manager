namespace Porchlight.Core.Browsers;

/// <summary>Lists (read-only) the add-ons installed in every supported browser profile.</summary>
public interface IBrowserExtensionScanner
{
    /// <summary>Scans on a background thread. Unreadable files are skipped, never thrown.</summary>
    Task<BrowserScanResult> ScanAsync(CancellationToken cancellationToken);
}
