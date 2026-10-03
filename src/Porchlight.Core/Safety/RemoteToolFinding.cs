namespace Porchlight.Core.Safety;

/// <summary>A remote-control tool found on this PC.</summary>
/// <param name="Id">Catalog id.</param>
/// <param name="Name">Name shown to the user.</param>
/// <param name="IsRunning">True when one of its programs is running right now.</param>
/// <param name="SetUpByPorchlight">True for the AnyDesk that Porchlight's Remote support page manages.</param>
public sealed record RemoteToolFinding(string Id, string Name, bool IsRunning, bool SetUpByPorchlight)
{
    /// <summary>Plain status line.</summary>
    public string StatusText => IsRunning ? "Running now" : "Installed, not running";
}
