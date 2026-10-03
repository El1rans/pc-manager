namespace Porchlight.Core.Printing;

/// <summary>All steps of one "Fix my printer" run.</summary>
public sealed record PrinterFixReport(IReadOnlyList<PrinterFixStep> Steps)
{
    public bool NeedsAdmin => Steps.Any(s => s.Status == PrinterFixStatus.NeedsAdmin);

    public bool HasProblem => Steps.Any(s => s.Status is PrinterFixStatus.Failed or PrinterFixStatus.NeedsAdmin);

    public string Summary =>
        NeedsAdmin ? "Some steps need administrator rights. Restart Porchlight as administrator and try again."
        : HasProblem ? "Not everything could be fixed. Check the printer itself: power, paper and cables."
        : "Done. Try printing again.";
}
