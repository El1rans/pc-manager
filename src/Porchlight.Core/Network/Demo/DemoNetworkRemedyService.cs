#if DEBUG
namespace Porchlight.Core.Network.Demo;

/// <summary>DEBUG-only <see cref="INetworkRemedyService"/> that pretends to succeed and runs nothing.</summary>
internal sealed class DemoNetworkRemedyService : INetworkRemedyService
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);

    public Task<RemedyResult> FlushDnsAsync(CancellationToken cancellationToken) => Pretend(cancellationToken);

    public Task<RemedyResult> RenewIpAsync(CancellationToken cancellationToken) => Pretend(cancellationToken);

    public Task<RemedyResult> ResetAdapterAsync(string adapterId, CancellationToken cancellationToken) =>
        Pretend(cancellationToken);

    private static async Task<RemedyResult> Pretend(CancellationToken cancellationToken)
    {
        await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
        return new RemedyResult(RemedyOutcome.Done, "Done.");
    }
}
#endif
