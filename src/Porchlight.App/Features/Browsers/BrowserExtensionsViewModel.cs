using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Shell;
using Porchlight.Core.Browsers;

namespace Porchlight.App.Features.Browsers;

/// <summary>
/// "Browser add-ons" page: a read-only list of the add-ons in Edge, Chrome, Brave and Firefox with
/// a plain-language risk note each. It never changes anything in a browser - see
/// docs/specs/17-browser-extensions.md.
/// </summary>
public sealed partial class BrowserExtensionsViewModel : PageViewModelBase, IDisposable
{
    private readonly IBrowserExtensionScanner _scanner;
    private readonly IBrowserHijackScanner _hijackScanner;
    private readonly IBrowserAddOnsOpener _opener;
    private readonly ILogger<BrowserExtensionsViewModel> _logger;

    private CancellationTokenSource? _scanCts;
    private BrowserScanResult? _lastResult;
    private BrowserHijackResult? _lastHijack;
    private bool _disposed;

    [ObservableProperty]
    private IReadOnlyList<BrowserSectionViewModel> _sections = [];

    [ObservableProperty]
    private string _summary = "Looking at your browsers...";

    [ObservableProperty]
    private string _hijackSummary = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasLoaded;

    [ObservableProperty]
    private bool _showOnlyReview;

    [ObservableProperty]
    private string? _openMessage;

    public BrowserExtensionsViewModel(
        IBrowserExtensionScanner scanner,
        IBrowserHijackScanner hijackScanner,
        IBrowserAddOnsOpener opener,
        ILogger<BrowserExtensionsViewModel> logger)
    {
        _scanner = scanner;
        _hijackScanner = hijackScanner;
        _opener = opener;
        _logger = logger;
    }

    public override string Title => "Browser add-ons";

    // Segoe Fluent Icons "Puzzle" glyph.
    public override string Glyph => "";

    public override int Order => 2;

    public override PageCategory Category => PageCategory.InternetAndSafety;

    public bool ShowEmptyState => HasLoaded && !HasError && Sections.Count == 0;

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        var cts = _scanCts = new CancellationTokenSource();

        IsBusy = true;
        HasError = false;
        try
        {
            _lastHijack = await ScanSettingsAsync(cts.Token).ConfigureAwait(true);
            _lastResult = await _scanner.ScanAsync(cts.Token).ConfigureAwait(true);
            HasLoaded = true;
            Rebuild();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer scan or the app is closing; nothing to show.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Browser add-on scan failed.");
            HasError = true;
            HasLoaded = true;
            Summary = "We couldn't look at your browsers just now. Please try again.";
            Sections = [];
        }
        finally
        {
            if (ReferenceEquals(_scanCts, cts))
            {
                IsBusy = false;
            }

            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    /// <summary>The settings check is a bonus on this page: if it fails the add-on list still shows.</summary>
    private async Task<BrowserHijackResult?> ScanSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _hijackScanner.ScanAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Browser settings check failed.");
            return null;
        }
    }

    partial void OnShowOnlyReviewChanged(bool value) => Rebuild();

    private void Rebuild()
    {
        if (_lastResult is not { } result)
        {
            return;
        }

        var sections = new List<BrowserSectionViewModel>();
        foreach (var browser in result.BrowsersFound)
        {
            var all = result.Extensions.Where(e => e.Extension.Browser == browser).ToList();
            var shown = all
                .Where(e => !ShowOnlyReview || e.Risk.Level != ExtensionRiskLevel.LooksFine)
                .OrderByDescending(e => e.Risk.Level)
                .ThenBy(e => e.Extension.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(e => new ExtensionItemViewModel(e))
                .ToList();
            var openCommand = new RelayCommand(() => OpenAddOnsPage(browser));
            var hijackRows = (_lastHijack?.Findings ?? [])
                .Where(f => f.Browser == browser)
                .Select(f => new HijackRowViewModel(f, new RelayCommand(() => OpenSettingsPage(browser, f.Setting))))
                .ToList();
            sections.Add(new BrowserSectionViewModel(browser, shown, all.Count, openCommand, hijackRows));
        }

        Sections = sections;
        Summary = BuildSummary(result);
        HijackSummary = BuildHijackSummary(_lastHijack);
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private static string BuildSummary(BrowserScanResult result)
    {
        if (result.BrowsersFound.Count == 0)
        {
            return "We couldn't find Edge, Chrome, Brave or Firefox on this PC.";
        }

        var total = result.Extensions.Count;
        var review = result.Extensions.Count(e => e.Risk.Level != ExtensionRiskLevel.LooksFine);
        var browsers = result.BrowsersFound.Count == 1 ? "1 browser" : $"{result.BrowsersFound.Count} browsers";
        var found = total == 1 ? "1 add-on" : $"{total} add-ons";
        var tail = review == 0 ? "None of them needs a second look." : review == 1 ? "1 is worth a look." : $"{review} are worth a look.";
        return $"We found {found} in {browsers}. {tail}";
    }

    private static string BuildHijackSummary(BrowserHijackResult? hijack)
    {
        if (hijack is null || hijack.Findings.Count == 0)
        {
            return string.Empty;
        }

        var forced = hijack.Findings.Count(f => f.Status == HijackStatus.ForcedByPolicy);
        var changed = hijack.Findings.Count(f => f.Status == HijackStatus.Changed);
        var total = forced + changed;
        if (total == 0)
        {
            return "Your home page, start-up pages and search engine look normal.";
        }

        var settings = total == 1 ? "1 browser setting looks changed" : $"{total} browser settings look changed";
        return forced > 0
            ? $"{settings}, and a setting on this PC is forcing {(forced == 1 ? "one of them" : "some of them")}. See each browser below."
            : $"{settings}. See each browser below.";
    }

    private void OpenSettingsPage(BrowserKind browser, HijackSetting setting)
    {
        var opened = _opener.TryOpenSettings(browser, setting);
        OpenMessage = opened
            ? null
            : $"We couldn't open {BrowserSectionViewModel.DisplayName(browser)} automatically. Open it, then type "
                + $"{BrowserAddOnsOpener.SettingsUrl(browser, setting)} in the address bar.";
    }

    private void OpenAddOnsPage(BrowserKind browser)
    {
        var opened = _opener.TryOpen(browser);
        OpenMessage = opened
            ? null
            : $"We couldn't open {BrowserSectionViewModel.DisplayName(browser)} automatically. Open it, then type "
                + $"{BrowserAddOnsOpener.AddOnsUrl(browser)} in the address bar.";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanCts?.Cancel();
        _scanCts?.Dispose();
    }
}
