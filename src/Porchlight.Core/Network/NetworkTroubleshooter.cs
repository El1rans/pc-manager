namespace Porchlight.Core.Network;

/// <inheritdoc cref="INetworkTroubleshooter"/>
public sealed class NetworkTroubleshooter : INetworkTroubleshooter
{
    private readonly INetworkProbe _probe;

    public NetworkTroubleshooter(INetworkProbe probe)
    {
        _probe = probe;
    }

    public async Task<TroubleshootReport> RunAsync(
        IProgress<TroubleshootStepUpdate>? progress, CancellationToken cancellationToken)
    {
        var results = new Dictionary<TroubleshootStep, StepState>();

        void Report(TroubleshootStep step, StepState state)
        {
            results[step] = state;
            progress?.Report(new TroubleshootStepUpdate(step, state, TroubleshootStepText.Describe(step, state)));
        }

        Report(TroubleshootStep.Adapter, StepState.Running);
        var adapterUp = await Task.Run(_probe.IsAdapterUp, cancellationToken).ConfigureAwait(false);
        Report(TroubleshootStep.Adapter, adapterUp ? StepState.Passed : StepState.Failed);

        if (!adapterUp)
        {
            Report(TroubleshootStep.Router, StepState.Skipped);
            Report(TroubleshootStep.NameLookup, StepState.Skipped);
            Report(TroubleshootStep.Internet, StepState.Skipped);
            return Finish(results);
        }

        Report(TroubleshootStep.Router, StepState.Running);
        var routerOk = await _probe.PingGatewayAsync(cancellationToken).ConfigureAwait(false);
        Report(TroubleshootStep.Router, routerOk ? StepState.Passed : StepState.Failed);

        Report(TroubleshootStep.NameLookup, StepState.Running);
        var dnsOk = await _probe.ResolveDnsAsync(cancellationToken).ConfigureAwait(false);
        Report(TroubleshootStep.NameLookup, dnsOk ? StepState.Passed : StepState.Failed);

        Report(TroubleshootStep.Internet, StepState.Running);
        var internet = await _probe.CheckInternetAsync(cancellationToken).ConfigureAwait(false);
        Report(TroubleshootStep.Internet, internet switch
        {
            InternetCheckResult.Reachable => StepState.Passed,
            InternetCheckResult.CaptivePortal => StepState.NeedsSignIn,
            _ => StepState.Failed,
        });

        return Finish(results);
    }

    private static TroubleshootReport Finish(Dictionary<TroubleshootStep, StepState> results) =>
        new(results, NetworkDiagnosis.Diagnose(results));
}
