using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Safety;
using Porchlight.Core.Startup;
using Porchlight.Core.WebConsole;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public sealed class WebConsoleDetailsCollectorTests : IDisposable
{
    private static readonly DateTimeOffset CheckedAt = new(2026, 10, 3, 9, 30, 0, TimeSpan.Zero);

    private readonly PendingUpdatesTracker _tracker = new();
    private readonly FakeStartup _startup = new();
    private readonly FakeSafety _safety = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly WebConsoleDetailsCollector _collector;

    public WebConsoleDetailsCollectorTests()
    {
        _collector = new WebConsoleDetailsCollector(
            _tracker, _startup, _safety, NullLogger<WebConsoleDetailsCollector>.Instance, _time);
    }

    public void Dispose() => _collector.Dispose();

    [Fact]
    public async Task Updates_before_any_check_say_so_without_running_anything()
    {
        var updates = await _collector.GetUpdatesAsync(TestContext.Current.CancellationToken);

        Assert.False(updates.HasChecked);
        Assert.Null(updates.CheckedAt);
        Assert.Empty(updates.Items);
    }

    [Fact]
    public async Task Updates_list_what_the_last_check_found()
    {
        _tracker.Report([new PendingUpdate("Firefox", "130.0", "131.0"), new PendingUpdate("7-Zip", "23.01", "24.08")], CheckedAt);

        var updates = await _collector.GetUpdatesAsync(TestContext.Current.CancellationToken);

        Assert.True(updates.HasChecked);
        Assert.Equal(CheckedAt, updates.CheckedAt);
        Assert.Equal(2, updates.Count);
        Assert.Equal(["Firefox", "7-Zip"], updates.Items.Select(i => i.Name).ToArray());
        Assert.Equal("131.0", updates.Items[0].AvailableVersion);
    }

    [Fact]
    public async Task Updates_from_a_count_only_report_still_show_the_count()
    {
        _tracker.Report(3, CheckedAt);

        var updates = await _collector.GetUpdatesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, updates.Count);
        Assert.Empty(updates.Items);
    }

    [Fact]
    public async Task Startup_is_sorted_by_impact_then_enabled_then_name_and_hides_paths()
    {
        _startup.Entries =
        [
            Entry("Zeta", StartupImpact.Low, enabled: true),
            Entry("Beta", StartupImpact.NotMeasured, enabled: true),
            Entry("Alpha", StartupImpact.High, enabled: false),
            Entry("Gamma", StartupImpact.High, enabled: true),
        ];
        _startup.NeedsAdmin = true;

        var startup = await _collector.GetStartupAsync(TestContext.Current.CancellationToken);

        Assert.True(startup.ImpactNeedsAdmin);
        Assert.Equal(["Gamma", "Alpha", "Zeta", "Beta"], startup.Items.Select(i => i.Name).ToArray());
        Assert.DoesNotContain(@"C:\", JsonSerializer.Serialize(startup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_and_security_reads_are_shared_until_the_ttl_passes()
    {
        await _collector.GetStartupAsync(TestContext.Current.CancellationToken);
        await _collector.GetSecurityAsync(TestContext.Current.CancellationToken);
        _time.Advance(WebConsoleOptions.DetailsCacheTtl - TimeSpan.FromSeconds(1));
        await _collector.GetStartupAsync(TestContext.Current.CancellationToken);
        await _collector.GetSecurityAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _startup.ListCalls);
        Assert.Equal(1, _safety.Calls);

        _time.Advance(TimeSpan.FromSeconds(2));
        await _collector.GetStartupAsync(TestContext.Current.CancellationToken);
        await _collector.GetSecurityAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _startup.ListCalls);
        Assert.Equal(2, _safety.Calls);
    }

    [Fact]
    public async Task Security_maps_headline_products_and_remote_tools()
    {
        var security = await _collector.GetSecurityAsync(TestContext.Current.CancellationToken);

        Assert.Equal("1 thing to look at", security.Headline);
        Assert.Equal(SafetyLevel.Attention, security.Level);
        Assert.Equal("This PC is protected", security.Protection.Verdict);
        Assert.Equal(["Microsoft Defender Antivirus - on, up to date"], security.Protection.Lines);
        Assert.Equal("Windows Update looks fine", security.WindowsUpdate.Verdict);
        Assert.Equal(SafetyLevel.Attention, security.RemoteAccess.Level);
        Assert.Equal(
            ["TeamViewer - Running now", "AnyDesk - Installed, not running (Set up by Porchlight)"],
            security.RemoteAccess.Lines);
    }

    [Fact]
    public async Task A_failed_security_read_says_it_could_not_check_then_recovers()
    {
        _safety.Fail = true;

        var failed = await _collector.GetSecurityAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SafetyLevel.Unknown, failed.Level);
        Assert.Equal("Couldn't check this PC", failed.Headline);

        _safety.Fail = false;
        var recovered = await _collector.GetSecurityAsync(TestContext.Current.CancellationToken);

        Assert.Equal("1 thing to look at", recovered.Headline);
    }

    [Fact]
    public async Task A_failed_startup_read_keeps_the_last_good_list()
    {
        _startup.Entries = [Entry("Alpha", StartupImpact.Low, enabled: true)];
        await _collector.GetStartupAsync(TestContext.Current.CancellationToken);
        _time.Advance(WebConsoleOptions.DetailsCacheTtl + TimeSpan.FromSeconds(1));
        _startup.Fail = true;

        var startup = await _collector.GetStartupAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Alpha"], startup.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _collector.GetSecurityAsync(cts.Token));
    }

    private static StartupEntry Entry(string name, StartupImpact impact, bool enabled) =>
        new($"CurrentUserRun|{name}", StartupSource.CurrentUserRun, name, name, "<b>Publisher</b>", $@"C:\Apps\{name}.exe", "hint", false, enabled, impact);

    private sealed class FakeStartup : IStartupService
    {
        public IReadOnlyList<StartupEntry> Entries { get; set; } = [];

        public bool NeedsAdmin { get; set; }

        public bool Fail { get; set; }

        public int ListCalls { get; private set; }

        public bool ImpactNeedsAdmin => NeedsAdmin;

        public Task<IReadOnlyList<StartupEntry>> ListAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            return Fail ? throw new InvalidOperationException("boom") : Task.FromResult(Entries);
        }

        public Task<StartupChangeResult> SetEnabledAsync(string entryId, bool enabled, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The web console never changes anything.");
    }

    private sealed class FakeSafety : ISafetyStatusService
    {
        public bool Fail { get; set; }

        public int Calls { get; private set; }

        public Task<SafetyStatus> GetAsync(CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            if (Fail)
            {
                throw new InvalidOperationException("boom");
            }

            var security = new SecurityStatus(
                "This PC is protected",
                SafetyLevel.Good,
                [new SecurityProduct("Microsoft Defender Antivirus", SecurityProductKind.Antivirus, new ProductStateInfo(ProductRunState.On, true))]);
            var update = new WindowsUpdateStatus("Windows Update looks fine", SafetyLevel.Good, null, 0, false, ["Last installed today"]);
            var remote = new RemoteAccessStatus(
            [
                new RemoteToolFinding("teamviewer", "TeamViewer", true, false),
                new RemoteToolFinding("anydesk", "AnyDesk", false, true),
            ]);
            return Task.FromResult(new SafetyStatus(security, update, remote));
        }
    }
}
