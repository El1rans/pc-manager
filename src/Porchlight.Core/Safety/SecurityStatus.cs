namespace Porchlight.Core.Safety;

/// <summary>Result of the security card.</summary>
/// <param name="Verdict">One plain line, e.g. "This PC is protected".</param>
/// <param name="Level">Good / Attention / Unknown.</param>
/// <param name="Products">Antivirus and firewall products found (empty when unavailable).</param>
public sealed record SecurityStatus(string Verdict, SafetyLevel Level, IReadOnlyList<SecurityProduct> Products)
{
    public IEnumerable<string> Details => Products.Select(p => p.Detail);
}
