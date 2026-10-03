namespace Porchlight.Core.Safety;

/// <summary>Is the antivirus on and current, and is the firewall on?</summary>
public interface ISecurityStatusService
{
    /// <summary>Never throws for ordinary failures; an unavailable Security Center gives an Unknown result.</summary>
    Task<SecurityStatus> GetAsync(CancellationToken cancellationToken);
}
