namespace Porchlight.Core.Checkup;

/// <summary>A finished check-up: what <see cref="CheckupTextRenderer"/> and
/// <see cref="CheckupHtmlRenderer"/> turn into text or a web page.</summary>
/// <param name="GeneratedAt">When the report was built (local time).</param>
/// <param name="ComputerName">The PC's name, so the helper can tell which PC this is.</param>
/// <param name="Sections">Sections in display order.</param>
public sealed record CheckupReport(DateTimeOffset GeneratedAt, string ComputerName, IReadOnlyList<CheckupSectionResult> Sections)
{
    /// <summary>The worst severity of any section.</summary>
    public CheckupSeverity OverallSeverity =>
        Sections.Count == 0 ? CheckupSeverity.Ok : Sections.Max(s => s.Severity);
}
