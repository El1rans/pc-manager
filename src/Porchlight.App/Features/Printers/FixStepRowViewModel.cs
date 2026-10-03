using Porchlight.Core.Printing;

namespace Porchlight.App.Features.Printers;

/// <summary>One line of the "Fix my printer" report: an icon and the outcome in words.</summary>
public sealed class FixStepRowViewModel
{
    // Segoe Fluent Icons: CheckMark, Remove (dash), Warning, ErrorBadge.
    private const string FineGlyph = "\uE73E";
    private const string SkippedGlyph = "\uE738";
    private const string WarningGlyph = "\uE7BA";
    private const string FailedGlyph = "\uEA39";

    public FixStepRowViewModel(PrinterFixStep step)
    {
        Text = step.Message;
        Glyph = step.Status switch
        {
            PrinterFixStatus.Fine or PrinterFixStatus.Fixed => FineGlyph,
            PrinterFixStatus.Skipped => SkippedGlyph,
            PrinterFixStatus.NeedsAdmin => WarningGlyph,
            _ => FailedGlyph,
        };
    }

    public string Glyph { get; }

    public string Text { get; }
}
