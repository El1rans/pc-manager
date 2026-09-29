using Porchlight.Core.Network;

namespace Porchlight.Core.Tests.Network;

internal sealed class FakeNetworkProbe : INetworkProbe
{
    public bool AdapterUp { get; set; } = true;

    public bool Router { get; set; } = true;

    public bool Dns { get; set; } = true;

    public InternetCheckResult Internet { get; set; } = InternetCheckResult.Reachable;

    public List<string> Calls { get; } = [];

    public bool IsAdapterUp()
    {
        Calls.Add("adapter");
        return AdapterUp;
    }

    public Task<bool> PingGatewayAsync(CancellationToken cancellationToken)
    {
        Calls.Add("router");
        return Task.FromResult(Router);
    }

    public Task<bool> ResolveDnsAsync(CancellationToken cancellationToken)
    {
        Calls.Add("dns");
        return Task.FromResult(Dns);
    }

    public Task<InternetCheckResult> CheckInternetAsync(CancellationToken cancellationToken)
    {
        Calls.Add("internet");
        return Task.FromResult(Internet);
    }
}
