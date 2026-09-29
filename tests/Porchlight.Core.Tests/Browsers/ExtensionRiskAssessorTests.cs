using Porchlight.Core.Browsers;
using Xunit;

namespace Porchlight.Core.Tests.Browsers;

public sealed class ExtensionRiskAssessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static InstalledExtension Make(
        ExtensionSource source = ExtensionSource.Store,
        bool enabled = true,
        DateTimeOffset? installed = null,
        string[]? permissions = null,
        string[]? hosts = null) =>
        new(BrowserKind.Chrome, "Default", "id", "Name", string.Empty, "1", enabled,
            installed ?? Now.AddDays(-100), source, permissions ?? [], hosts ?? []);

    private static bool Has(ExtensionRiskAssessment a, ExtensionRiskFlagKind kind) =>
        a.Flags.Any(f => f.Kind == kind);

    [Fact]
    public void PlainStoreExtension_LooksFine()
    {
        var result = ExtensionRiskAssessor.Assess(Make(permissions: ["storage"]), Now);

        Assert.Equal(ExtensionRiskLevel.LooksFine, result.Level);
        Assert.Empty(result.Flags);
    }

    [Theory]
    [InlineData("<all_urls>")]
    [InlineData("*://*/*")]
    [InlineData("http://*/*")]
    [InlineData("https://*/*")]
    public void AllSitesHostPattern_IsFlagged(string pattern)
    {
        var result = ExtensionRiskAssessor.Assess(Make(hosts: [pattern]), Now);

        Assert.True(Has(result, ExtensionRiskFlagKind.AllSites));
        Assert.Equal(ExtensionRiskLevel.Review, result.Level);
    }

    [Fact]
    public void SingleSiteHost_IsNotFlagged()
    {
        var result = ExtensionRiskAssessor.Assess(Make(hosts: ["https://example.com/*"]), Now);

        Assert.False(Has(result, ExtensionRiskFlagKind.AllSites));
    }

    [Theory]
    [InlineData("history", ExtensionRiskFlagKind.History)]
    [InlineData("tabs", ExtensionRiskFlagKind.History)]
    [InlineData("webNavigation", ExtensionRiskFlagKind.History)]
    [InlineData("downloads", ExtensionRiskFlagKind.Downloads)]
    [InlineData("nativeMessaging", ExtensionRiskFlagKind.NativeMessaging)]
    [InlineData("proxy", ExtensionRiskFlagKind.Proxy)]
    [InlineData("debugger", ExtensionRiskFlagKind.Debugger)]
    [InlineData("management", ExtensionRiskFlagKind.Management)]
    public void Permission_MapsToFlag(string permission, ExtensionRiskFlagKind expected)
    {
        var result = ExtensionRiskAssessor.Assess(Make(permissions: [permission]), Now);

        Assert.True(Has(result, expected));
    }

    [Fact]
    public void DownloadsOnly_IsANoteAndStillLooksFine()
    {
        var result = ExtensionRiskAssessor.Assess(Make(permissions: ["downloads"]), Now);

        Assert.Equal(ExtensionRiskLevel.LooksFine, result.Level);
    }

    [Fact]
    public void RecentlyInstalled_IsANoteOnly()
    {
        var result = ExtensionRiskAssessor.Assess(Make(installed: Now.AddDays(-3)), Now);

        Assert.True(Has(result, ExtensionRiskFlagKind.RecentlyInstalled));
        Assert.Equal(ExtensionRiskLevel.LooksFine, result.Level);
    }

    [Fact]
    public void InstalledEightDaysAgo_IsNotRecent()
    {
        var result = ExtensionRiskAssessor.Assess(Make(installed: Now.AddDays(-8)), Now);

        Assert.False(Has(result, ExtensionRiskFlagKind.RecentlyInstalled));
    }

    [Theory]
    [InlineData(ExtensionSource.Sideloaded)]
    [InlineData(ExtensionSource.Developer)]
    [InlineData(ExtensionSource.Unknown)]
    public void NonStoreSource_IsFlaggedAndReview(ExtensionSource source)
    {
        var result = ExtensionRiskAssessor.Assess(Make(source: source), Now);

        Assert.True(Has(result, ExtensionRiskFlagKind.NotFromStore));
        Assert.Equal(ExtensionRiskLevel.Review, result.Level);
    }

    [Fact]
    public void Policy_IsFlaggedWithExplanation()
    {
        var result = ExtensionRiskAssessor.Assess(Make(source: ExtensionSource.Policy), Now);

        var flag = Assert.Single(result.Flags);
        Assert.Equal(ExtensionRiskFlagKind.Policy, flag.Kind);
        Assert.Equal(ExtensionRiskLevel.Review, result.Level);
    }

    [Fact]
    public void SideloadedWithAllSites_IsWorthRemoving()
    {
        var result = ExtensionRiskAssessor.Assess(Make(ExtensionSource.Sideloaded, hosts: ["<all_urls>"]), Now);

        Assert.Equal(ExtensionRiskLevel.WorthRemoving, result.Level);
    }

    [Fact]
    public void PolicyWithProxy_IsWorthRemoving()
    {
        var result = ExtensionRiskAssessor.Assess(Make(ExtensionSource.Policy, permissions: ["proxy"]), Now);

        Assert.Equal(ExtensionRiskLevel.WorthRemoving, result.Level);
    }

    [Fact]
    public void DisabledSideloadedPowerful_IsCappedAtReview()
    {
        var result = ExtensionRiskAssessor.Assess(
            Make(ExtensionSource.Sideloaded, enabled: false, hosts: ["<all_urls>"]), Now);

        Assert.Equal(ExtensionRiskLevel.Review, result.Level);
    }

    [Fact]
    public void StoreExtensionWithManyPermissions_IsReviewNotWorthRemoving()
    {
        var result = ExtensionRiskAssessor.Assess(
            Make(permissions: ["history", "proxy"], hosts: ["<all_urls>"]), Now);

        Assert.Equal(ExtensionRiskLevel.Review, result.Level);
    }

    [Fact]
    public void Wording_NeverClaimsMalware()
    {
        var result = ExtensionRiskAssessor.Assess(
            Make(ExtensionSource.Policy, permissions: ["history", "downloads", "proxy", "debugger", "management", "nativeMessaging"], hosts: ["<all_urls>"]),
            Now);

        Assert.All(result.Flags, f =>
        {
            Assert.DoesNotContain("malware", f.Title + f.Explanation, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("virus", f.Title + f.Explanation, StringComparison.OrdinalIgnoreCase);
        });
    }
}
