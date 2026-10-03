namespace Porchlight.Core.Safety;

/// <summary>Whether the built-in Windows Firewall is on, per network profile.</summary>
/// <param name="Profiles">Domain, Private and Public with their on/off state.</param>
/// <param name="ActiveProfiles">The profile(s) the PC is using right now (can be several flags).</param>
public sealed record WindowsFirewallStatus(
    IReadOnlyDictionary<WindowsFirewallProfile, bool> Profiles,
    WindowsFirewallProfile ActiveProfiles)
{
    /// <summary>True when the firewall is on for every active profile. When Windows reports no active
    /// profile, every profile has to be on (the safe reading).</summary>
    public bool IsOnForActiveProfiles
    {
        get
        {
            var relevant = Profiles
                .Where(p => ActiveProfiles == WindowsFirewallProfile.None || ActiveProfiles.HasFlag(p.Key))
                .ToList();
            return relevant.Count > 0 && relevant.All(p => p.Value);
        }
    }
}
