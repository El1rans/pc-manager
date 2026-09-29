namespace Porchlight.Core.Network;

/// <summary>Pure grouping of connections into per-program rows.</summary>
public static class NetworkAppUsageAggregator
{
    /// <summary>Groups <paramref name="connections"/> by the name <paramref name="nameOf"/> gives
    /// each process (connections whose process has no name are dropped), busiest first, then by name.</summary>
    public static IReadOnlyList<NetworkAppUsage> Aggregate(
        IReadOnlyList<NetworkConnection> connections, Func<int, string?> nameOf)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(nameOf);

        var byPid = connections.GroupBy(c => c.Pid);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in byPid)
        {
            var name = nameOf(group.Key);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            counts[name] = counts.GetValueOrDefault(name) + group.Count();
        }

        return counts
            .Select(p => new NetworkAppUsage(p.Key, p.Value))
            .OrderByDescending(a => a.ConnectionCount)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
