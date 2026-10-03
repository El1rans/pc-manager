namespace Porchlight.Core.Safety;

/// <summary>Turns the Security Center products into one plain verdict line. Pure.</summary>
public static class SecurityVerdictBuilder
{
    public const string Protected = "This PC is protected";
    public const string AntivirusOff = "Antivirus is off";
    public const string AntivirusPaused = "Antivirus is paused";
    public const string AntivirusOutOfDate = "Antivirus is out of date";
    public const string NoAntivirus = "No antivirus found";
    public const string FirewallOff = "Firewall is off";
    public const string CouldNotCheck = "Couldn't check this PC's security status";

    /// <summary>Builds the verdict. <paramref name="products"/> null means Security Center is unavailable.</summary>
    public static SecurityStatus Build(IReadOnlyList<SecurityProduct>? products)
    {
        if (products is null)
        {
            return new SecurityStatus(CouldNotCheck, SafetyLevel.Unknown, []);
        }

        var antivirus = products.Where(p => p.Kind == SecurityProductKind.Antivirus).ToList();
        var firewalls = products.Where(p => p.Kind == SecurityProductKind.Firewall).ToList();

        var verdict = AntivirusVerdict(antivirus) ?? FirewallVerdict(firewalls);
        return verdict is null
            ? new SecurityStatus(Protected, SafetyLevel.Good, products)
            : new SecurityStatus(verdict, SafetyLevel.Attention, products);
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

    private static string? FirewallVerdict(List<SecurityProduct> firewalls) =>
        firewalls.Count > 0 && !firewalls.Any(p => p.State.State == ProductRunState.On) ? FirewallOff : null;
}
