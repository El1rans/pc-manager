namespace Porchlight.Core.Safety.Demo;

/// <summary>DEBUG demo data: Defender on and current, firewall on.</summary>
internal sealed class DemoSecurityStatusService : ISecurityStatusService
{
    public Task<SecurityStatus> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(SecurityVerdictBuilder.Build(
        [
            new SecurityProduct("Windows Defender", SecurityProductKind.Antivirus, new ProductStateInfo(ProductRunState.On, true)),
            new SecurityProduct("Windows Firewall", SecurityProductKind.Firewall, new ProductStateInfo(ProductRunState.On, null)),
        ]));
}
