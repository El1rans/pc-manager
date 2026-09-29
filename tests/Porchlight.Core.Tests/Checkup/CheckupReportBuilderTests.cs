using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Checkup;
using Porchlight.Core.Checkup.Sections;
using Porchlight.Core.Components;
using Porchlight.Core.Monitoring;
using Porchlight.Core.RemoteSupport;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Checkup;

public sealed class CheckupReportBuilderTests
{
    private const string SecretDriveLabel = "Grandma-Photos-Secret";
    private const string SecretAnyDeskId = "987654321";
    private const string SecretAppName = "SecretInstalledApp";
    private const string SecretUserName = "zelda-fake-user";

    private sealed class FakeSystemInfo : ISystemInfoProvider
    {
        public Task<SystemInfo> GetAsync(CancellationToken cancellationToken) => Task.FromResult(
            new SystemInfo(
                "TEST-PC", "Windows 11 Pro", "26200", "Acme", "Box 1", "Some CPU", 4, 8, ["Some GPU"],
                16L * 1024 * 1024 * 1024, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    private sealed class FakeRestartDetector(bool pending) : IRestartDetector
    {
        public bool IsRestartPending() => pending;
    }

    private sealed class FakeDriveMonitor : IDriveMonitor
    {
        public IReadOnlyList<DriveSnapshot> GetDrives() =>
        [
            new DriveSnapshot(@"C:\", SecretDriveLabel, "NTFS", 500L * 1024 * 1024 * 1024, 100L * 1024 * 1024 * 1024, false),
            new DriveSnapshot(@"D:\", null, "NTFS", 100L * 1024 * 1024 * 1024, 1L * 1024 * 1024 * 1024, true),
        ];
    }

    private sealed class FakeAnyDesk : IAnyDeskService
    {
        private static AnyDeskState State() =>
            new(true, @"C:\Users\" + SecretUserName + @"\AnyDesk.exe", "9.0", SecretAnyDeskId, "alias-" + SecretAnyDeskId, true,
                new ComponentStatus(ComponentState.Running));

        public Task<AnyDeskState> GetStateAsync(CancellationToken cancellationToken) => Task.FromResult(State());

        public Task<AnyDeskState> LaunchAsync(CancellationToken cancellationToken) => Task.FromResult(State());
    }

    private sealed class ThrowingSection : ICheckupSection
    {
        public string Title => "Broken";

        public int Order => 50;

        public Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class FixedSection(int order, string title) : ICheckupSection
    {
        public string Title => title;

        public int Order => order;

        public Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken) =>
            Task.FromResult<CheckupSectionResult?>(new CheckupSectionResult(title, CheckupSeverity.Ok, ["fine"]));
    }

    private static List<ICheckupSection> BuiltIn(bool restartPending = false)
    {
        var tracker = new PendingUpdatesTracker();
        tracker.Report(3, new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero));
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
        return
        [
            new ComputerCheckupSection(new FakeSystemInfo()),
            new RestartCheckupSection(new FakeSystemInfo(), new FakeRestartDetector(restartPending), time),
            new DriveSpaceCheckupSection(new FakeDriveMonitor()),
            new AppUpdatesCheckupSection(tracker),
            new RemoteHelpCheckupSection(new FakeAnyDesk()),
        ];
    }

    private static CheckupReportBuilder Builder(IEnumerable<ICheckupSection> sections) =>
        new(sections, TimeProvider.System, NullLogger<CheckupReportBuilder>.Instance);

    [Fact]
    public async Task Output_NeverContainsSensitiveValues()
    {
        var report = await Builder(BuiltIn()).BuildAsync(TestContext.Current.CancellationToken);
        var outputs = new[] { CheckupTextRenderer.Render(report), CheckupHtmlRenderer.Render(report) };

        foreach (var output in outputs)
        {
            Assert.DoesNotContain(SecretDriveLabel, output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SecretAnyDeskId, output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SecretAppName, output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(@"\Users\", output, StringComparison.OrdinalIgnoreCase);
            if (SecretUserName.Length > 3)
            {
                Assert.DoesNotContain(SecretUserName, output, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task BuiltInSections_ReportExpectedSeverities()
    {
        var report = await Builder(BuiltIn(restartPending: true)).BuildAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckupSeverity.Problem, report.Sections.Single(s => s.Title == "Drive space").Severity);
        Assert.Equal(CheckupSeverity.NeedsAttention, report.Sections.Single(s => s.Title == "Restarts").Severity);
        Assert.Contains("3 apps have updates", report.Sections.Single(s => s.Title == "App updates").Lines[0]);
        Assert.Equal(CheckupSeverity.Ok, report.Sections.Single(s => s.Title == "Remote help").Severity);
        Assert.Equal(CheckupSeverity.Problem, report.OverallSeverity);
    }

    [Fact]
    public async Task FailingSection_DoesNotStopReport_AndSectionsAreOrdered()
    {
        var sections = new List<ICheckupSection> { new FixedSection(900, "Late"), new ThrowingSection(), new FixedSection(10, "Early") };

        var report = await Builder(sections).BuildAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Early", "Broken", "Late"], report.Sections.Select(s => s.Title));
        Assert.Equal(CheckupSeverity.NeedsAttention, report.Sections[1].Severity);
    }

    [Fact]
    public void Renderers_EncodeHtml_AndUseSeverityWords()
    {
        var report = new CheckupReport(
            DateTimeOffset.UnixEpoch, "PC<1>", [new CheckupSectionResult("A & B", CheckupSeverity.Problem, ["<script>x</script>"])]);

        var html = CheckupHtmlRenderer.Render(report);
        var text = CheckupTextRenderer.Render(report);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("Problem", html);
        Assert.Contains("A & B - Problem", text);
        Assert.Contains("Overall: Problem", text);
    }
}
