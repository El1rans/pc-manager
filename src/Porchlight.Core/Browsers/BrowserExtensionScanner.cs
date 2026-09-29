using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Browsers;

/// <inheritdoc cref="IBrowserExtensionScanner"/>
public sealed class BrowserExtensionScanner : IBrowserExtensionScanner
{
    private readonly IBrowserLocations _locations;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BrowserExtensionScanner> _logger;

    public BrowserExtensionScanner(
        IBrowserLocations locations,
        TimeProvider timeProvider,
        ILogger<BrowserExtensionScanner> logger)
    {
        _locations = locations;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<BrowserScanResult> ScanAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private BrowserScanResult Scan(CancellationToken cancellationToken)
    {
        var tally = new ScanTally();
        var found = new List<BrowserKind>();
        var installed = new List<InstalledExtension>();

        var chromium = new ChromiumExtensionReader(_logger, tally);
        foreach (var location in _locations.ChromiumBrowsers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ChromiumExtensionReader.IsInstalled(location))
            {
                continue;
            }

            found.Add(location.Kind);
            try
            {
                installed.AddRange(chromium.Read(location, cancellationToken));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read {Browser} profiles.", location.Kind);
                tally.CountSkipped();
            }
        }

        var firefoxDir = _locations.FirefoxProfilesDirectory;
        if (firefoxDir is not null && FirefoxExtensionReader.IsInstalled(firefoxDir))
        {
            found.Add(BrowserKind.Firefox);
            try
            {
                installed.AddRange(new FirefoxExtensionReader(_logger, tally).Read(firefoxDir, cancellationToken));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read Firefox profiles.");
                tally.CountSkipped();
            }
        }

        var now = _timeProvider.GetUtcNow();
        var assessed = installed
            .Select(e => new AssessedExtension(e, ExtensionRiskAssessor.Assess(e, now)))
            .ToList();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Browser add-on scan: {Count} add-ons in {Browsers} browsers, {Skipped} skipped.",
                assessed.Count,
                found.Count,
                tally.Skipped);
        }

        return new BrowserScanResult(assessed, found, tally.Skipped);
    }
}
