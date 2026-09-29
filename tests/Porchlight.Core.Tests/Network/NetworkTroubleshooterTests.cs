using Porchlight.Core.Network;
using Xunit;

namespace Porchlight.Core.Tests.Network;

public class NetworkTroubleshooterTests
{
    private sealed class ListProgress<T> : IProgress<T>
    {
        public List<T> Items { get; } = [];

        public void Report(T value) => Items.Add(value);
    }

    [Fact]
    public async Task Runs_steps_in_order_and_diagnoses_healthy()
    {
        var probe = new FakeNetworkProbe();
        var progress = new ListProgress<TroubleshootStepUpdate>();

        var report = await new NetworkTroubleshooter(probe).RunAsync(progress, TestContext.Current.CancellationToken);

        Assert.Equal(["adapter", "router", "dns", "internet"], probe.Calls);
        Assert.Equal(DiagnosisOutcome.Healthy, report.Diagnosis.Outcome);
        Assert.Equal(StepState.Running, progress.Items[0].State);
        Assert.Equal(TroubleshootStep.Adapter, progress.Items[0].Step);
        Assert.All(report.Steps.Values, s => Assert.Equal(StepState.Passed, s));
    }

    [Fact]
    public async Task Adapter_down_skips_the_rest()
    {
        var probe = new FakeNetworkProbe { AdapterUp = false };

        var report = await new NetworkTroubleshooter(probe).RunAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(["adapter"], probe.Calls);
        Assert.Equal(StepState.Skipped, report.Steps[TroubleshootStep.Internet]);
        Assert.Equal(DiagnosisOutcome.AdapterDown, report.Diagnosis.Outcome);
    }

    [Fact]
    public async Task Dns_failure_is_diagnosed()
    {
        var probe = new FakeNetworkProbe { Dns = false, Internet = InternetCheckResult.Unreachable };

        var report = await new NetworkTroubleshooter(probe).RunAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(DiagnosisOutcome.DnsProblem, report.Diagnosis.Outcome);
    }

    [Fact]
    public async Task Captive_portal_is_diagnosed_as_sign_in()
    {
        var probe = new FakeNetworkProbe { Internet = InternetCheckResult.CaptivePortal };

        var report = await new NetworkTroubleshooter(probe).RunAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(StepState.NeedsSignIn, report.Steps[TroubleshootStep.Internet]);
        Assert.Equal(DiagnosisOutcome.SignInRequired, report.Diagnosis.Outcome);
    }
}
