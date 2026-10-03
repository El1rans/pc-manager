namespace Porchlight.Core.Safety;

/// <inheritdoc cref="ISecurityStatusService"/>
public sealed class SecurityStatusService(ISecurityCenterReader reader, IWindowsFirewallReader firewallReader) : ISecurityStatusService
{
    public async Task<SecurityStatus> GetAsync(CancellationToken cancellationToken)
    {
        var products = reader.ReadAsync(cancellationToken);
        var firewall = firewallReader.ReadAsync(cancellationToken);
        return SecurityVerdictBuilder.Build(
            await products.ConfigureAwait(false),
            await firewall.ConfigureAwait(false));
    }
}
