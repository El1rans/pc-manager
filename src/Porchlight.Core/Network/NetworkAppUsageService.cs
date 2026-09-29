namespace Porchlight.Core.Network;

/// <inheritdoc cref="INetworkAppUsageService"/>
public sealed class NetworkAppUsageService : INetworkAppUsageService
{
    private readonly INetworkConnectionReader _reader;
    private readonly IProcessNameResolver _names;

    public NetworkAppUsageService(INetworkConnectionReader reader, IProcessNameResolver names)
    {
        _reader = reader;
        _names = names;
    }

    public IReadOnlyList<NetworkAppUsage> GetUsage() =>
        NetworkAppUsageAggregator.Aggregate(_reader.ReadConnections(), _names.GetFriendlyName);
}
