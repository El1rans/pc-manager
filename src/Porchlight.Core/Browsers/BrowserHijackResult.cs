namespace Porchlight.Core.Browsers;

/// <summary>Outcome of one check of the start, new-tab and search settings of every browser.</summary>
/// <param name="Findings">One entry per setting checked.</param>
/// <param name="BrowsersChecked">Browsers that appear to be installed.</param>
/// <param name="SkippedCount">Files that could not be read and were skipped.</param>
public sealed record BrowserHijackResult(
    IReadOnlyList<HijackFinding> Findings,
    IReadOnlyList<BrowserKind> BrowsersChecked,
    int SkippedCount);
