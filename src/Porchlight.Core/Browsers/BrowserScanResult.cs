namespace Porchlight.Core.Browsers;

/// <summary>Outcome of one scan of every browser profile.</summary>
/// <param name="Extensions">Every add-on found.</param>
/// <param name="BrowsersFound">Browsers that appear to be installed (have a profile folder).</param>
/// <param name="SkippedCount">Files or entries that could not be read and were skipped.</param>
public sealed record BrowserScanResult(
    IReadOnlyList<AssessedExtension> Extensions,
    IReadOnlyList<BrowserKind> BrowsersFound,
    int SkippedCount);
