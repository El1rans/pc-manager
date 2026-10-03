namespace Porchlight.Core.Safety;

/// <summary>Matches what was found on the PC against the known remote-tool table. Pure.</summary>
public static class RemoteToolMatcher
{
    private const string ExeSuffix = ".exe";

    public static IReadOnlyList<RemoteToolFinding> Match(
        RemoteToolEvidence evidence, IEnumerable<RemoteToolDefinition>? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var processes = new HashSet<string>(
            evidence.ProcessNames.Select(StripExe), StringComparer.OrdinalIgnoreCase);

        var findings = new List<RemoteToolFinding>();
        foreach (var tool in catalog ?? RemoteToolCatalog.All)
        {
            var running = tool.ProcessNames.Any(processes.Contains);
            var installed =
                ContainsAny(evidence.InstalledAppNames, tool.AppNameFragments) ||
                ContainsAny(evidence.ServiceNames, tool.ServiceNameFragments);
            if (running || installed)
            {
                findings.Add(new RemoteToolFinding(tool.Id, tool.Name, running, SetUpByPorchlight: false));
            }
        }

        return findings;
    }

    private static string StripExe(string name) =>
        name.EndsWith(ExeSuffix, StringComparison.OrdinalIgnoreCase) ? name[..^ExeSuffix.Length] : name;

    private static bool ContainsAny(IEnumerable<string> names, IReadOnlyList<string> fragments) =>
        fragments.Count > 0 &&
        names.Any(n => fragments.Any(f => n.Contains(f, StringComparison.OrdinalIgnoreCase)));
}
