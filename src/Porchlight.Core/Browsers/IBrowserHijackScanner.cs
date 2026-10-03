namespace Porchlight.Core.Browsers;

/// <summary>Reads (never changes) the home page, startup pages, new-tab page and search engine of every
/// supported browser, plus any policy that forces them.</summary>
public interface IBrowserHijackScanner
{
    /// <summary>Scans on a background thread. Unreadable files are skipped, never thrown.</summary>
    Task<BrowserHijackResult> ScanAsync(CancellationToken cancellationToken);
}
