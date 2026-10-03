using Porchlight.Core.Safety;
using Xunit;

namespace Porchlight.Core.Tests.Safety;

public sealed class SecurityVerdictBuilderTests
{
    private static SecurityProduct Av(ProductRunState state, bool? current = true) =>
        new("Antivirus", SecurityProductKind.Antivirus, new ProductStateInfo(state, current));

    private static SecurityProduct Fw(ProductRunState state) =>
        new("Firewall", SecurityProductKind.Firewall, new ProductStateInfo(state, null));

    [Fact]
    public void Unavailable_IsUnknown()
    {
        var status = SecurityVerdictBuilder.Build(null);

        Assert.Equal(SecurityVerdictBuilder.CouldNotCheck, status.Verdict);
        Assert.Equal(SafetyLevel.Unknown, status.Level);
    }

    [Fact]
    public void AllGood_IsProtected()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On), Fw(ProductRunState.On)]);

        Assert.Equal(SecurityVerdictBuilder.Protected, status.Verdict);
        Assert.Equal(SafetyLevel.Good, status.Level);
    }

    [Fact]
    public void AntivirusOff_Wins()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.Off), Fw(ProductRunState.Off)]);

        Assert.Equal(SecurityVerdictBuilder.AntivirusOff, status.Verdict);
        Assert.Equal(SafetyLevel.Attention, status.Level);
    }

    [Fact]
    public void AntivirusSnoozed_IsPaused()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.Snoozed), Fw(ProductRunState.On)]);

        Assert.Equal(SecurityVerdictBuilder.AntivirusPaused, status.Verdict);
    }

    [Fact]
    public void AntivirusOnButStale_IsOutOfDate()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On, current: false), Fw(ProductRunState.On)]);

        Assert.Equal(SecurityVerdictBuilder.AntivirusOutOfDate, status.Verdict);
    }

    [Fact]
    public void DefenderOff_ThirdPartyOn_IsProtected()
    {
        var status = SecurityVerdictBuilder.Build(
            [Av(ProductRunState.Off), Av(ProductRunState.On), Fw(ProductRunState.On)]);

        Assert.Equal(SecurityVerdictBuilder.Protected, status.Verdict);
    }

    [Fact]
    public void FirewallOff_IsReported()
    {
        var status = SecurityVerdictBuilder.Build([Av(ProductRunState.On), Fw(ProductRunState.Off)]);

        Assert.Equal(SecurityVerdictBuilder.FirewallOff, status.Verdict);
    }

    [Fact]
    public void NoAntivirusProducts_IsReported()
    {
        var status = SecurityVerdictBuilder.Build([Fw(ProductRunState.On)]);

        Assert.Equal(SecurityVerdictBuilder.NoAntivirus, status.Verdict);
        Assert.Equal(SafetyLevel.Attention, status.Level);
    }

    [Fact]
    public void ProductDetail_IsPlain()
    {
        Assert.Equal("Antivirus - on, up to date", Av(ProductRunState.On).Detail);
        Assert.Equal("Antivirus - on, out of date", Av(ProductRunState.On, false).Detail);
        Assert.Equal("Firewall - off", Fw(ProductRunState.Off).Detail);
    }
}
