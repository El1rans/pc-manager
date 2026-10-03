namespace Porchlight.Core.Safety;

/// <inheritdoc cref="ISecurityStatusService"/>
public sealed class SecurityStatusService(ISecurityCenterReader reader) : ISecurityStatusService
{
    public async Task<SecurityStatus> GetAsync(CancellationToken cancellationToken) =>
        SecurityVerdictBuilder.Build(await reader.ReadAsync(cancellationToken).ConfigureAwait(false));
}
