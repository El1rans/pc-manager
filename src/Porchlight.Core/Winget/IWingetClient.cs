namespace Porchlight.Core.Winget;

/// <summary>
/// Runs <c>winget</c> for the Updates page: listing available upgrades, upgrading one package, and
/// showing a package's details. Built on the shared <see cref="Processes.IProcessRunner"/> (see
/// <c>docs/specs/01b-components.md</c>) rather than starting <c>winget.exe</c> itself.
/// </summary>
public interface IWingetClient
{
    /// <summary>
    /// Runs <c>winget upgrade --accept-source-agreements --disable-interactivity</c> (plus
    /// <c>--include-unknown</c> when requested) and parses its output with
    /// <see cref="WingetTableParser"/>.
    /// </summary>
    /// <param name="includeUnknown">Also list packages whose installed version winget could not
    /// determine (shown as <c>"Unknown"</c>).</param>
    /// <param name="progress">Receives winget's redrawn progress/status text while it checks for
    /// updates; may be null.</param>
    Task<IReadOnlyList<WingetPackage>> GetUpgradesAsync(
        bool includeUnknown, IProgress<string>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <c>winget upgrade --id &lt;id&gt; --exact --include-unknown
    /// --accept-package-agreements --accept-source-agreements --disable-interactivity</c> (plus
    /// <c>--silent</c> when requested).
    /// </summary>
    /// <param name="cancellationToken">
    /// Passed straight through to <see cref="Processes.IProcessRunner.RunAsync"/>, which kills the
    /// whole process tree on cancellation - not a graceful "let it finish" request. The Updates
    /// page's "Stop after current" therefore never cancels a package that is already updating; the
    /// view model passes <see cref="CancellationToken.None"/> here for the duration of each
    /// in-flight upgrade and only checks its own "stop requested" flag between packages.
    /// </param>
    Task<WingetResult> UpgradeAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken);

    /// <summary>Runs <c>winget show --id &lt;id&gt; --exact</c>, streaming its output to
    /// <paramref name="log"/> (used by the Updates page's "Show package info" context menu item).</summary>
    Task<WingetResult> ShowAsync(string id, IProgress<string>? log, CancellationToken cancellationToken);
}
