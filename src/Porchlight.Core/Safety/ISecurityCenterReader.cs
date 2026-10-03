namespace Porchlight.Core.Safety;

/// <summary>Reads Windows Security Center. The only class that talks to WMI for this card.</summary>
public interface ISecurityCenterReader
{
    /// <summary>Reads the antivirus and firewall products. Returns null when Security Center is not
    /// available on this PC (Windows Server, service stopped, access denied).</summary>
    Task<IReadOnlyList<SecurityProduct>?> ReadAsync(CancellationToken cancellationToken);
}
