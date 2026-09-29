using System.Globalization;
using Porchlight.Core.Health;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Health;

/// <summary>One physical disk row.</summary>
public sealed class DiskRowViewModel
{
    public DiskRowViewModel(DiskHealthReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Name = report.Name;

        var parts = new List<string> { report.MediaTypeText, ByteFormatter.FormatBytes(report.SizeBytes) };
        if (report.TemperatureCelsius is { } temp)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{temp} °C"));
        }

        if (report.WearPercent is { } wear)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{wear}% of life used"));
        }

        Detail = string.Join(" - ", parts);
        VerdictText = report.Assessment.VerdictText;
        ReasonsText = string.Join(" ", report.Assessment.Reasons);
        Severity = report.Assessment.Verdict switch
        {
            DiskHealthVerdict.Healthy => HealthSeverity.Ok,
            DiskHealthVerdict.Warning => HealthSeverity.Warning,
            _ => HealthSeverity.Neutral,
        };
        Glyph = HealthGlyphs.For(Severity);
    }

    public string Name { get; }

    public string Detail { get; }

    public string VerdictText { get; }

    public string ReasonsText { get; }

    public bool HasReasons => ReasonsText.Length > 0;

    public HealthSeverity Severity { get; }

    public string Glyph { get; }
}
