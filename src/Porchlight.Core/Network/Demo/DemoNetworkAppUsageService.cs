#if DEBUG
namespace Porchlight.Core.Network.Demo;

/// <summary>DEBUG-only <see cref="INetworkAppUsageService"/> with a made-up program list.</summary>
internal sealed class DemoNetworkAppUsageService : INetworkAppUsageService
{
    private static readonly IReadOnlyList<NetworkAppUsage> Apps =
    [
        new NetworkAppUsage("Web browser", 14),
        new NetworkAppUsage("Email", 3),
        new NetworkAppUsage("Music player", 2),
        new NetworkAppUsage("Windows (system)", 2),
    ];

    public IReadOnlyList<NetworkAppUsage> GetUsage() => Apps;
}
#endif
