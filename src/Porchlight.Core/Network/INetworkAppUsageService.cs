namespace Porchlight.Core.Network;

/// <summary>Which programs currently have internet connections. Read-only.</summary>
public interface INetworkAppUsageService
{
    /// <summary>Returns the current per-program connection counts. Call off the UI thread.</summary>
    IReadOnlyList<NetworkAppUsage> GetUsage();
}
