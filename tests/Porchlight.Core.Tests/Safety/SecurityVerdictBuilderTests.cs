using Porchlight.Core.Safety;
using Xunit;

namespace Porchlight.Core.Tests.Safety;

public sealed class SecurityVerdictBuilderTests
{
    private static SecurityProduct Av(ProductRunState state, bool? current = true) =>
        new("Antivirus", SecurityProductKind.Antivirus, new ProductStateInfo(state, current));

    private static SecurityProduct Fw(ProductRunState state, string name = "Firewall") =>
        new(name, SecurityProductKind.Firewall, new ProductStateInfo(state, null));

    private static WindowsFirewallStatus Win(bool domain, bool priv, bool pub, WindowsFirewallProfile active) =>
        new(
            new Dictionary<WindowsFirewallProfile, bool>
            {
                [WindowsFirewallProfile.Domain] = domain,
                [WindowsFirewallProfile.Private] = priv,
                [WindowsFirewallProfile.Public] = pub,
            },
            active);

    private static readonly WindowsFirewallStatus WindowsOn = Win(true, true, true, WindowsFirewallProfile.Private);

    private static readonly WindowsFirewallStatus WindowsOff = Win(false, false, false, WindowsFirewallProfile.Private);

    [Fact]
    public void Unavailable_IsUnknown()
    {
        var status = SecurityVerdictBuilder.Build(null, null);

        Assert.Equal(SecurityVerdictBuilder.CouldNotCheck, status.Verdict);
        Assert.Equal(SafetyLevel.Unknown, status.Level);
    }

    [Fact]
    public void AllGood_IsProtected()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On), Fw(ProductRunState.On)], null);

        Assert.Equal(SecurityVerdictBuilder.Protected, status.Verdict);
        Assert.Equal(SafetyLevel.Good, status.Level);
    }

    [Fact]
    public void AntivirusOff_Wins()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.Off), Fw(ProductRunState.Off)], WindowsOff);

        Assert.Equal(SecurityVerdictBuilder.AntivirusOff, status.Verdict);
        Assert.Equal(SafetyLevel.Attention, status.Level);
    }

    [Fact]
    public void AntivirusSnoozed_IsPaused()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.Snoozed), Fw(ProductRunState.On)], WindowsOn);

        Assert.Equal(SecurityVerdictBuilder.AntivirusPaused, status.Verdict);
    }

    [Fact]
    public void AntivirusOnButStale_IsOutOfDate()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On, current: false), Fw(ProductRunState.On)], WindowsOn);

        Assert.Equal(SecurityVerdictBuilder.AntivirusOutOfDate, status.Verdict);
    }

    [Fact]
    public void DefenderOff_ThirdPartyOn_IsProtected()
    {
        var status = SecurityVerdictBuilder.Build(
            [Av(ProductRunState.Off), Av(ProductRunState.On), Fw(ProductRunState.On)], WindowsOn);

        Assert.Equal(SecurityVerdictBuilder.Protected, status.Verdict);
    }

    [Fact]
    public void NoAntivirusProducts_IsReported()
    {
        var status = SecurityVerdictBuilder.Build([Fw(ProductRunState.On)], WindowsOn);

        Assert.Equal(SecurityVerdictBuilder.NoAntivirus, status.Verdict);
        Assert.Equal(SafetyLevel.Attention, status.Level);
    }

    [Fact]
    public void NoFirewallProduct_WindowsFirewallOn_IsProtected()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On)], WindowsOn);

        Assert.Equal(SecurityVerdictBuilder.Protected, status.Verdict);
        Assert.Contains("Windows Firewall - on", status.Details);
    }

    [Fact]
    public void NoFirewallProduct_WindowsFirewallOff_WarnsEvenThoughSecurityCenterIsSilent()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On)], WindowsOff);

        Assert.Equal(SecurityVerdictBuilder.FirewallOff, status.Verdict);
        Assert.Equal(SafetyLevel.Attention, status.Level);
        Assert.Contains("Windows Firewall - off", status.Details);
    }

    [Fact]
    public void WindowsFirewallOff_ThirdPartyOn_IsProtected()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On), Fw(ProductRunState.On)], WindowsOff);

        Assert.Equal(SecurityVerdictBuilder.Protected, status.Verdict);
    }

    [Fact]
    public void ThirdPartyOff_WindowsFirewallOff_Warns()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On), Fw(ProductRunState.Off)], WindowsOff);

        Assert.Equal(SecurityVerdictBuilder.FirewallOff, status.Verdict);
    }

    [Fact]
    public void ThirdPartyOff_WindowsFirewallOn_IsProtected()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On), Fw(ProductRunState.Off)], WindowsOn);

        Assert.Equal(SecurityVerdictBuilder.Protected, status.Verdict);
    }

    [Fact]
    public void OnlyTheActiveProfileCounts()
    {
        // Public profile is off but the PC is on a private network.
        var privateOnly = Win(false, true, false, WindowsFirewallProfile.Private);
        var onPrivate = SecurityVerdictBuilder.Build([Av(ProductRunState.On)], privateOnly);
        var onPublic = SecurityVerdictBuilder.Build([Av(ProductRunState.On)], privateOnly with { ActiveProfiles = WindowsFirewallProfile.Public });

        Assert.Equal(SecurityVerdictBuilder.Protected, onPrivate.Verdict);
        Assert.Equal(SecurityVerdictBuilder.FirewallOff, onPublic.Verdict);
    }

    [Fact]
    public void SeveralActiveProfiles_AllMustBeOn()
    {
        var status = SecurityVerdictBuilder.Build(
            [Av(ProductRunState.On)],
            Win(true, false, true, WindowsFirewallProfile.Domain | WindowsFirewallProfile.Private));

        Assert.Equal(SecurityVerdictBuilder.FirewallOff, status.Verdict);
    }

    [Fact]
    public void NoActiveProfileReported_EveryProfileMustBeOn()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On)], Win(true, true, false, WindowsFirewallProfile.None));

        Assert.Equal(SecurityVerdictBuilder.FirewallOff, status.Verdict);
    }

    [Fact]
    public void WindowsFirewallUnreadable_NoOtherFirewallInfo_IsCouldNotCheck()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On)], null);

        Assert.Equal(SecurityVerdictBuilder.CouldNotCheck, status.Verdict);
        Assert.Equal(SafetyLevel.Unknown, status.Level);
    }

    [Fact]
    public void WindowsFirewallUnreadable_UsesWindowsFirewallListedBySecurityCenter()
    {
        var on = SecurityVerdictBuilder.Build([Av(ProductRunState.On), Fw(ProductRunState.On, "Windows Firewall")], null);
        var off = SecurityVerdictBuilder.Build([Av(ProductRunState.On), Fw(ProductRunState.Off, "Windows Firewall")], null);

        Assert.Equal(SecurityVerdictBuilder.Protected, on.Verdict);
        Assert.Equal(SecurityVerdictBuilder.FirewallOff, off.Verdict);
    }

    [Fact]
    public void WindowsFirewallListedBySecurityCenter_IsNotShownTwice()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On), Fw(ProductRunState.On, "Windows Firewall")], WindowsOn);

        Assert.Single(status.Products, p => p.Kind == SecurityProductKind.Firewall);
    }

    [Fact]
    public void SecurityCenterUnavailable_WindowsFirewallOff_StillWarns()
    {
        var status = SecurityVerdictBuilder.Build(null, WindowsOff);

        Assert.Equal(SecurityVerdictBuilder.FirewallOff, status.Verdict);
        Assert.Equal(SafetyLevel.Attention, status.Level);
    }

    [Fact]
    public void SecurityCenterUnavailable_WindowsFirewallOn_IsCouldNotCheck()
    {
        var status = SecurityVerdictBuilder.Build(null, WindowsOn);

        Assert.Equal(SecurityVerdictBuilder.CouldNotCheck, status.Verdict);
        Assert.Equal(SafetyLevel.Unknown, status.Level);
    }

    [Fact]
    public void ProductDetail_IsPlain()
    {
        Assert.Equal("Antivirus - on, up to date", Av(ProductRunState.On).Detail);
        Assert.Equal("Antivirus - on, out of date", Av(ProductRunState.On, false).Detail);
        Assert.Equal("Firewall - off", Fw(ProductRunState.Off).Detail);
    }
}
