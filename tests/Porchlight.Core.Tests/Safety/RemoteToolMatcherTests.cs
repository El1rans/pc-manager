using Porchlight.Core.Safety;
using Xunit;

namespace Porchlight.Core.Tests.Safety;

public sealed class RemoteToolMatcherTests
{
    private static RemoteToolEvidence Evidence(
        string[]? processes = null, string[]? apps = null, string[]? services = null) =>
        new(processes ?? [], apps ?? [], services ?? []);

    [Fact]
    public void NothingFound_ReturnsEmpty()
    {
        var findings = RemoteToolMatcher.Match(Evidence(["chrome", "explorer"], ["Mozilla Firefox"], ["Spooler"]));

        Assert.Empty(findings);
    }

    [Fact]
    public void RunningProcess_MatchesIgnoringCaseAndExe()
    {
        var findings = RemoteToolMatcher.Match(Evidence(["teamviewer.EXE"]));

        var finding = Assert.Single(findings);
        Assert.Equal("TeamViewer", finding.Name);
        Assert.True(finding.IsRunning);
    }

    [Fact]
    public void InstalledApp_MatchesByDisplayNameFragment_NotRunning()
    {
        var findings = RemoteToolMatcher.Match(Evidence(apps: ["RustDesk 1.2.3", "7-Zip"]));

        var finding = Assert.Single(findings);
        Assert.Equal("rustdesk", finding.Id);
        Assert.False(finding.IsRunning);
    }

    [Fact]
    public void ServiceName_MatchesScreenConnectInstance()
    {
        var findings = RemoteToolMatcher.Match(Evidence(services: ["ScreenConnect Client (a1b2c3d4e5f6)"]));

        var finding = Assert.Single(findings);
        Assert.Equal("connectwise", finding.Id);
    }

    [Fact]
    public void QuickAssist_OnlyMatchesWhileRunning()
    {
        var findings = RemoteToolMatcher.Match(Evidence(["QuickAssist"]));

        var finding = Assert.Single(findings);
        Assert.Equal(RemoteToolCatalog.QuickAssistId, finding.Id);
    }

    [Fact]
    public void SeveralEvidenceSources_GiveOneFindingPerTool()
    {
        var findings = RemoteToolMatcher.Match(Evidence(["AnyDesk"], ["AnyDesk"], ["AnyDesk"]));

        Assert.Single(findings);
    }

    [Fact]
    public void SeveralTools_AreAllReported()
    {
        var findings = RemoteToolMatcher.Match(Evidence(
            ["SRManager"], ["UltraViewer", "Supremo"], ["chromoting"]));

        string[] expected = ["chromeremotedesktop", "splashtop", "supremo", "ultraviewer"];
        Assert.Equal(expected, findings.Select(f => f.Id).Order().ToArray());
    }

    [Fact]
    public void Catalog_HasEveryRequestedTool()
    {
        string[] expected =
        [
            "TeamViewer", "AnyDesk", "RustDesk", "UltraViewer", "Splashtop",
            "Chrome Remote Desktop", "LogMeIn", "ConnectWise ScreenConnect", "Supremo", "Quick Assist",
        ];

        Assert.Equal(expected.Order(), RemoteToolCatalog.All.Select(t => t.Name).Order());
    }
}
