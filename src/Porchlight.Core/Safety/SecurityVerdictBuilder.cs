namespace Porchlight.Core.Safety;

/// <summary>Turns the Security Center products and the Windows Firewall state into one plain verdict
/// line. Pure.</summary>
public static class SecurityVerdictBuilder
{
    public const string Protected = "This PC is protected";
    public const string AntivirusOff = "Antivirus is off";
    public const string AntivirusPaused = "Antivirus is paused";
    public const string AntivirusOutOfDate = "Antivirus is out of date";
    public const string NoAntivirus = "No antivirus found";
    public const string FirewallOff = "Windows Firewall is off";
    public const string CouldNotCheck = "Couldn't check this PC's security status";

    private const string WindowsFirewallName = "Windows Firewall";
    private const string WindowsDefenderFirewallName = "Windows Defender Firewall";

    /// <summary>Builds the verdict. <paramref name="products"/> null means Security Center is unavailable;
    /// <paramref name="windowsFirewall"/> null means the built-in firewall could not be read.</summary>
    public static SecurityStatus Build(IReadOnlyList<SecurityProduct>? products, WindowsFirewallStatus? windowsFirewall)
    {
        var firewalls = (products ?? []).Where(p => p.Kind == SecurityProductKind.Firewall).ToList();
        var firewallProtected = FirewallProtected(firewalls, windowsFirewall);
        var shown = WithWindowsFirewall(products ?? [], windowsFirewall);

        if (products is null)
        {
            // Antivirus can't be judged. A firewall that is off is still worth saying.
            return firewallProtected == false
                ? new SecurityStatus(FirewallOff, SafetyLevel.Attention, shown)
                : new SecurityStatus(CouldNotCheck, SafetyLevel.Unknown, shown);
        }

        var antivirus = products.Where(p => p.Kind == SecurityProductKind.Antivirus).ToList();
        var verdict = AntivirusVerdict(antivirus) ?? (firewallProtected == false ? FirewallOff : null);
        if (verdict is not null)
        {
            return new SecurityStatus(verdict, SafetyLevel.Attention, shown);
        }

        return firewallProtected is null
            ? new SecurityStatus(CouldNotCheck, SafetyLevel.Unknown, shown)
            : new SecurityStatus(Protected, SafetyLevel.Good, shown);
    }

    /// <summary>True when a third-party firewall is on or Windows Firewall is on for the active
    /// profile(s); false when neither is; null when nothing could be read.</summary>
    private static bool? FirewallProtected(List<SecurityProduct> firewalls, WindowsFirewallStatus? windowsFirewall)
    {
        if (firewalls.Any(p => !IsWindowsFirewall(p) && p.State.State == ProductRunState.On))
        {
            return true;
        }

        if (windowsFirewall is not null)
        {
            return windowsFirewall.IsOnForActiveProfiles;
        }

        // The built-in firewall could not be read; Security Center may still list it.
        var listed = firewalls.Where(IsWindowsFirewall).ToList();
        if (listed.Count > 0)
        {
            return listed.Any(p => p.State.State == ProductRunState.On);
        }

        return firewalls.Count > 0 ? false : null;
    }

    private static bool IsWindowsFirewall(SecurityProduct product) =>
        product.Name.Contains(WindowsFirewallName, StringComparison.OrdinalIgnoreCase)
        || product.Name.Contains(WindowsDefenderFirewallName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Adds a "Windows Firewall - on/off" detail line unless Security Center already lists it.</summary>
    private static List<SecurityProduct> WithWindowsFirewall(IReadOnlyList<SecurityProduct> products, WindowsFirewallStatus? windowsFirewall)
    {
        var all = products.ToList();
        if (windowsFirewall is not null && !all.Any(p => p.Kind == SecurityProductKind.Firewall && IsWindowsFirewall(p)))
        {
            var state = windowsFirewall.IsOnForActiveProfiles ? ProductRunState.On : ProductRunState.Off;
            all.Add(new SecurityProduct(WindowsFirewallName, SecurityProductKind.Firewall, new ProductStateInfo(state, null)));
        }

        return all;
    }

    /// <summary>Null when antivirus is fine. One product that is on and current is enough: Windows turns
    /// Defender off by itself when another antivirus takes over.</summary>
    private static string? AntivirusVerdict(List<SecurityProduct> antivirus)
    {
        if (antivirus.Count == 0)
        {
            return NoAntivirus;
        }

        var running = antivirus.Where(p => p.State.State == ProductRunState.On).ToList();
        if (running.Count == 0)
        {
            return antivirus.Any(p => p.State.State == ProductRunState.Snoozed) ? AntivirusPaused : AntivirusOff;
        }

        return running.Any(p => p.State.DefinitionsUpToDate != false) ? null : AntivirusOutOfDate;
    }
}
