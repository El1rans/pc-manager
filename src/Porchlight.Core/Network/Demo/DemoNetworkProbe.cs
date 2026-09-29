#if DEBUG
namespace Porchlight.Core.Network.Demo;

/// <summary>DEBUG-only <see cref="INetworkProbe"/> where every check passes after a short pause.</summary>
internal sealed class DemoNetworkProbe : INetworkProbe
{
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(600);

    public bool IsAdapterUp() => true;

    public async Task<bool> PingGatewayAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(StepDelay, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> ResolveDnsAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(StepDelay, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<InternetCheckResult> CheckInternetAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(StepDelay, cancellationToken).ConfigureAwait(false);
        return InternetCheckResult.Reachable;
    }
}
#endif
