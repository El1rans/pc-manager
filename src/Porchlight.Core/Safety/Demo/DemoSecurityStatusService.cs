namespace Porchlight.Core.Safety.Demo;

/// <summary>DEBUG demo data: Defender on and current, Windows Firewall on.</summary>
internal sealed class DemoSecurityStatusService : ISecurityStatusService
{
    public Task<SecurityStatus> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(SecurityVerdictBuilder.Build(
            [new SecurityProduct("Windows Defender", SecurityProductKind.Antivirus, new ProductStateInfo(ProductRunState.On, true))],
            new WindowsFirewallStatus(
                new Dictionary<WindowsFirewallProfile, bool>
                {
                    [WindowsFirewallProfile.Domain] = true,
                    [WindowsFirewallProfile.Private] = true,
                    [WindowsFirewallProfile.Public] = true,
                },
                WindowsFirewallProfile.Private)));
}
