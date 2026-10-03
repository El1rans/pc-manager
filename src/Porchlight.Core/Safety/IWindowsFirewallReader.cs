namespace Porchlight.Core.Safety;

/// <summary>Reads the built-in Windows Firewall state (read-only; never changes a firewall setting).
/// Security Center usually lists only third-party firewalls, so this is the source for Windows' own.</summary>
public interface IWindowsFirewallReader
{
    /// <summary>Reads the per-profile state off the calling thread. Returns null when it can't be read.</summary>
    Task<WindowsFirewallStatus?> ReadAsync(CancellationToken cancellationToken);
}
