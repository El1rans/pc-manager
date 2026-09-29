using Porchlight.Core.Network;
using Xunit;

namespace Porchlight.Core.Tests.Network;

public class NetworkDiagnosisTests
{
    private static Diagnosis Run(StepState adapter, StepState router, StepState dns, StepState internet) =>
        NetworkDiagnosis.Diagnose(new Dictionary<TroubleshootStep, StepState>
        {
            [TroubleshootStep.Adapter] = adapter,
            [TroubleshootStep.Router] = router,
            [TroubleshootStep.NameLookup] = dns,
            [TroubleshootStep.Internet] = internet,
        });

    [Fact]
    public void All_passed_is_healthy_with_no_remedies()
    {
        var d = Run(StepState.Passed, StepState.Passed, StepState.Passed, StepState.Passed);
        Assert.Equal(DiagnosisOutcome.Healthy, d.Outcome);
        Assert.Empty(d.Remedies);
    }

    [Fact]
    public void Blocked_router_ping_is_still_healthy_when_internet_works()
    {
        var d = Run(StepState.Passed, StepState.Failed, StepState.Passed, StepState.Passed);
        Assert.Equal(DiagnosisOutcome.Healthy, d.Outcome);
    }

    [Fact]
    public void Sign_in_page_is_reported_without_remedies()
    {
        var d = Run(StepState.Passed, StepState.Passed, StepState.Passed, StepState.NeedsSignIn);
        Assert.Equal(DiagnosisOutcome.SignInRequired, d.Outcome);
        Assert.Empty(d.Remedies);
    }

    [Fact]
    public void Adapter_down_offers_reset_then_settings()
    {
        var d = Run(StepState.Failed, StepState.Skipped, StepState.Skipped, StepState.Skipped);
        Assert.Equal(DiagnosisOutcome.AdapterDown, d.Outcome);
        Assert.Equal([RemedyKind.ResetAdapter, RemedyKind.OpenNetworkSettings], d.Remedies);
    }

    [Fact]
    public void Router_unreachable_offers_renew_then_reset()
    {
        var d = Run(StepState.Passed, StepState.Failed, StepState.Failed, StepState.Failed);
        Assert.Equal(DiagnosisOutcome.RouterUnreachable, d.Outcome);
        Assert.Equal([RemedyKind.RenewIp, RemedyKind.ResetAdapter], d.Remedies);
    }

    [Fact]
    public void Dns_failure_offers_flush_first()
    {
        var d = Run(StepState.Passed, StepState.Passed, StepState.Failed, StepState.Failed);
        Assert.Equal(DiagnosisOutcome.DnsProblem, d.Outcome);
        Assert.Equal([RemedyKind.FlushDns, RemedyKind.RenewIp], d.Remedies);
    }

    [Fact]
    public void Internet_failure_with_router_and_dns_ok_is_internet_unreachable()
    {
        var d = Run(StepState.Passed, StepState.Passed, StepState.Passed, StepState.Failed);
        Assert.Equal(DiagnosisOutcome.InternetUnreachable, d.Outcome);
        Assert.Contains(RemedyKind.OpenNetworkSettings, d.Remedies);
        Assert.DoesNotContain(RemedyKind.ResetAdapter, d.Remedies);
    }

    [Fact]
    public void Missing_steps_count_as_skipped()
    {
        var d = NetworkDiagnosis.Diagnose(new Dictionary<TroubleshootStep, StepState>());
        Assert.Equal(DiagnosisOutcome.InternetUnreachable, d.Outcome);
    }

    [Fact]
    public void Only_the_last_resort_remedy_is_the_open_settings_one()
    {
        // Network reset itself is never a RemedyKind: it can only be offered by opening settings.
        Assert.DoesNotContain("NetworkReset", Enum.GetNames<RemedyKind>());
    }
}
