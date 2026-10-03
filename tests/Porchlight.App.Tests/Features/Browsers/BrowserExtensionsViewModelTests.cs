using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Browsers;
using Porchlight.Core.Browsers;
using Xunit;

namespace Porchlight.App.Tests.Features.Browsers;

public sealed class BrowserExtensionsViewModelTests : IDisposable
{
    private readonly FakeScanner _scanner = new();
    private readonly FakeOpener _opener = new();
    private readonly FakeHijackScanner _hijack = new();
    private readonly BrowserExtensionsViewModel _viewModel;

    public BrowserExtensionsViewModelTests()
    {
        _viewModel = new BrowserExtensionsViewModel(_scanner, _hijack, _opener, NullLogger<BrowserExtensionsViewModel>.Instance);
    }

    public void Dispose() => _viewModel.Dispose();

    private static AssessedExtension Extension(
        BrowserKind browser, string name, ExtensionRiskLevel level = ExtensionRiskLevel.LooksFine) =>
        new(
            new InstalledExtension(
                browser, "Default", name + "-id", name, string.Empty, "1.0", true, null, ExtensionSource.Store, [], []),
            new ExtensionRiskAssessment(level, []));

    private static BrowserScanResult Scan(IReadOnlyList<BrowserKind> browsers, params AssessedExtension[] extensions) =>
        new(extensions, browsers, 0);

    [Fact]
    public async Task Navigating_ScansAndShowsOneSectionPerBrowser()
    {
        _scanner.Result = Scan(
            [BrowserKind.Edge, BrowserKind.Firefox],
            Extension(BrowserKind.Edge, "Adblock"),
            Extension(BrowserKind.Firefox, "Dark Mode"));

        await _viewModel.OnNavigatedToAsync(CancellationToken.None);

        Assert.Equal(
            ["Microsoft Edge", "Firefox"], _viewModel.Sections.Select(s => s.Name));
        Assert.Equal("Adblock", Assert.Single(_viewModel.Sections[0].Items).Name);
        Assert.True(_viewModel.HasLoaded);
        Assert.False(_viewModel.HasError);
        Assert.False(_viewModel.IsBusy);
    }

    [Fact]
    public async Task WhileScanning_IsBusy()
    {
        var gate = new TaskCompletionSource<BrowserScanResult>();
        _scanner.Gates.Enqueue(gate);

        var scan = _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(_viewModel.IsBusy);

        gate.SetResult(Scan([]));
        await scan;
        Assert.False(_viewModel.IsBusy);
    }

    public static TheoryData<BrowserScanResult, string> SummaryCases => new()
    {
        { Scan([]), "We couldn't find Edge, Chrome, Brave or Firefox on this PC." },
        {
            Scan([BrowserKind.Edge], Extension(BrowserKind.Edge, "A")),
            "We found 1 add-on in 1 browser. None of them needs a second look."
        },
        {
            Scan(
                [BrowserKind.Edge, BrowserKind.Chrome],
                Extension(BrowserKind.Edge, "A", ExtensionRiskLevel.Review),
                Extension(BrowserKind.Chrome, "B"),
                Extension(BrowserKind.Chrome, "C")),
            "We found 3 add-ons in 2 browsers. 1 is worth a look."
        },
        {
            Scan(
                [BrowserKind.Chrome],
                Extension(BrowserKind.Chrome, "A", ExtensionRiskLevel.Review),
                Extension(BrowserKind.Chrome, "B", ExtensionRiskLevel.WorthRemoving)),
            "We found 2 add-ons in 1 browser. 2 are worth a look."
        },
    };

    [Theory]
    [MemberData(nameof(SummaryCases))]
    public async Task Summary_DescribesWhatWasFound(BrowserScanResult result, string expected)
    {
        _scanner.Result = result;

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(expected, _viewModel.Summary);
    }

    [Fact]
    public async Task Items_AreSortedWorstFirst_ThenByName()
    {
        _scanner.Result = Scan(
            [BrowserKind.Chrome],
            Extension(BrowserKind.Chrome, "zeta", ExtensionRiskLevel.LooksFine),
            Extension(BrowserKind.Chrome, "beta", ExtensionRiskLevel.Review),
            Extension(BrowserKind.Chrome, "Alpha", ExtensionRiskLevel.Review),
            Extension(BrowserKind.Chrome, "omega", ExtensionRiskLevel.WorthRemoving));

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["omega", "Alpha", "beta", "zeta"], _viewModel.Sections[0].Items.Select(i => i.Name));
    }

    [Fact]
    public async Task ShowOnlyReview_HidesAddOnsThatLookFine_ButKeepsTheTotalCount()
    {
        _scanner.Result = Scan(
            [BrowserKind.Chrome],
            Extension(BrowserKind.Chrome, "fine"),
            Extension(BrowserKind.Chrome, "risky", ExtensionRiskLevel.Review));
        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.ShowOnlyReview = true;

        var section = Assert.Single(_viewModel.Sections);
        Assert.Equal("risky", Assert.Single(section.Items).Name);
        Assert.Equal(2, section.TotalCount);
        Assert.Equal(1, _scanner.ScanCount);
    }

    [Fact]
    public void ShowOnlyReview_BeforeAnyScan_DoesNothing()
    {
        _viewModel.ShowOnlyReview = true;

        Assert.Empty(_viewModel.Sections);
        Assert.False(_viewModel.HasLoaded);
    }

    [Fact]
    public async Task ScanFailing_ShowsAFriendlyErrorInsteadOfAList()
    {
        _scanner.Failure = new IOException("disk");

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(_viewModel.HasError);
        Assert.True(_viewModel.HasLoaded);
        Assert.Empty(_viewModel.Sections);
        Assert.Equal("We couldn't look at your browsers just now. Please try again.", _viewModel.Summary);
        Assert.False(_viewModel.IsBusy);
        Assert.False(_viewModel.ShowEmptyState);
    }

    [Fact]
    public async Task ScanWithNoBrowsers_ShowsTheEmptyState()
    {
        _scanner.Result = Scan([]);

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(_viewModel.ShowEmptyState);
    }

    [Fact]
    public async Task ScanAfterAFailure_ClearsTheError()
    {
        _scanner.Failure = new IOException("disk");
        await _viewModel.RefreshCommand.ExecuteAsync(null);
        _scanner.Failure = null;
        _scanner.Result = Scan([BrowserKind.Edge], Extension(BrowserKind.Edge, "A"));

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.False(_viewModel.HasError);
        Assert.Single(_viewModel.Sections);
    }

    [Fact]
    public async Task NewScan_SupersedesAnUnfinishedOne_WhoseResultIsDiscarded()
    {
        var first = new TaskCompletionSource<BrowserScanResult>();
        var second = new TaskCompletionSource<BrowserScanResult>();
        _scanner.Gates.Enqueue(first);
        _scanner.Gates.Enqueue(second);

        var firstScan = _viewModel.RefreshCommand.ExecuteAsync(null);
        var secondScan = _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(_scanner.Tokens[0].IsCancellationRequested);
        first.SetCanceled(_scanner.Tokens[0]);
        await firstScan;
        Assert.True(_viewModel.IsBusy);
        Assert.False(_viewModel.HasLoaded);

        second.SetResult(Scan([BrowserKind.Brave], Extension(BrowserKind.Brave, "A")));
        await secondScan;

        Assert.Equal("Brave", Assert.Single(_viewModel.Sections).Name);
        Assert.False(_viewModel.IsBusy);
    }

    [Fact]
    public void Dispose_CancelsAScanInFlight_AndCanBeRepeated()
    {
        _scanner.Gates.Enqueue(new TaskCompletionSource<BrowserScanResult>());
        _ = _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Dispose();
        _viewModel.Dispose();

        Assert.True(_scanner.Tokens[0].IsCancellationRequested);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(
        false,
        "We couldn't open Google Chrome automatically. Open it, then type chrome://extensions in the address bar.")]
    public async Task OpeningABrowsersAddOnsPage_ReportsWhenItCouldNotBeOpened(bool opened, string? expectedMessage)
    {
        _opener.Succeeds = opened;
        _scanner.Result = Scan([BrowserKind.Chrome]);
        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Sections[0].OpenCommand.Execute(null);

        Assert.Equal([BrowserKind.Chrome], _opener.Opened);
        Assert.Equal(expectedMessage, _viewModel.OpenMessage);
    }

    [Fact]
    public async Task SettingsFindings_AreShownUnderTheirBrowser_WithASummary()
    {
        _scanner.Result = Scan([BrowserKind.Chrome, BrowserKind.Edge]);
        _hijack.Result = new BrowserHijackResult(
            [
                new(BrowserKind.Chrome, "Default", HijackSetting.HomePage, HijackStatus.Changed, "fast-search.example"),
                new(BrowserKind.Chrome, null, HijackSetting.SearchEngine, HijackStatus.ForcedByPolicy, "evil.example"),
                new(BrowserKind.Edge, "Default", HijackSetting.HomePage, HijackStatus.Ok, "msn.com"),
            ],
            [BrowserKind.Chrome, BrowserKind.Edge],
            0);

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        var chrome = _viewModel.Sections[0].HijackRows;
        Assert.Equal(2, chrome.Count);
        Assert.Equal("Changed", chrome[0].StatusText);
        Assert.Equal("Forced by a setting on this PC", chrome[1].StatusText);
        Assert.Equal("Home page", chrome[0].SettingText);
        Assert.Contains("fast-search.example", chrome[0].Details, StringComparison.Ordinal);
        Assert.True(chrome[0].HasAdvice);
        Assert.Contains("reset it in the browser's settings", chrome[0].Advice, StringComparison.Ordinal);
        Assert.False(_viewModel.Sections[1].HijackRows[0].HasAdvice);
        Assert.Equal(
            "2 browser settings look changed, and a setting on this PC is forcing one of them. See each browser below.",
            _viewModel.HijackSummary);
    }

    [Fact]
    public async Task SettingsAllFine_SaysSo()
    {
        _scanner.Result = Scan([BrowserKind.Edge]);
        _hijack.Result = new BrowserHijackResult(
            [new(BrowserKind.Edge, "Default", HijackSetting.SearchEngine, HijackStatus.Ok, "bing.com")],
            [BrowserKind.Edge],
            0);

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Your home page, start-up pages and search engine look normal.", _viewModel.HijackSummary);
    }

    [Fact]
    public async Task SettingsCheckFailing_StillShowsTheAddOns()
    {
        _scanner.Result = Scan([BrowserKind.Edge], Extension(BrowserKind.Edge, "A"));
        _hijack.Failure = new IOException("disk");

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.False(_viewModel.HasError);
        Assert.Single(_viewModel.Sections[0].Items);
        Assert.Empty(_viewModel.Sections[0].HijackRows);
        Assert.Equal(string.Empty, _viewModel.HijackSummary);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(
        false,
        "We couldn't open Google Chrome automatically. Open it, then type chrome://settings/search in the address bar.")]
    public async Task OpeningSettings_ReportsWhenItCouldNotBeOpened(bool opened, string? expectedMessage)
    {
        _opener.Succeeds = opened;
        _scanner.Result = Scan([BrowserKind.Chrome]);
        _hijack.Result = new BrowserHijackResult(
            [new(BrowserKind.Chrome, "Default", HijackSetting.SearchEngine, HijackStatus.Changed, "x.example")],
            [BrowserKind.Chrome],
            0);
        await _viewModel.RefreshCommand.ExecuteAsync(null);

        _viewModel.Sections[0].HijackRows[0].OpenCommand.Execute(null);

        Assert.Equal([(BrowserKind.Chrome, HijackSetting.SearchEngine)], _opener.OpenedSettings);
        Assert.Equal(expectedMessage, _viewModel.OpenMessage);
    }

    private sealed class FakeHijackScanner : IBrowserHijackScanner
    {
        public BrowserHijackResult Result { get; set; } = new([], [], 0);

        public Exception? Failure { get; set; }

        public Task<BrowserHijackResult> ScanAsync(CancellationToken cancellationToken) =>
            Failure is null ? Task.FromResult(Result) : throw Failure;
    }

    private sealed class FakeScanner : IBrowserExtensionScanner
    {
        public BrowserScanResult Result { get; set; } = new([], [], 0);

        public Exception? Failure { get; set; }

        public Queue<TaskCompletionSource<BrowserScanResult>> Gates { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public int ScanCount { get; private set; }

        public Task<BrowserScanResult> ScanAsync(CancellationToken cancellationToken)
        {
            ScanCount++;
            Tokens.Add(cancellationToken);
            if (Gates.TryDequeue(out var gate))
            {
                return gate.Task;
            }

            return Failure is null ? Task.FromResult(Result) : throw Failure;
        }
    }

    private sealed class FakeOpener : IBrowserAddOnsOpener
    {
        public bool Succeeds { get; set; } = true;

        public List<BrowserKind> Opened { get; } = [];

        public List<(BrowserKind Browser, HijackSetting Setting)> OpenedSettings { get; } = [];

        public bool TryOpen(BrowserKind kind)
        {
            Opened.Add(kind);
            return Succeeds;
        }

        public bool TryOpenSettings(BrowserKind kind, HijackSetting setting)
        {
            OpenedSettings.Add((kind, setting));
            return Succeeds;
        }
    }
}
