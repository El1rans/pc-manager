namespace Porchlight.Core.Printing;

/// <summary>Result of a printer change; <see cref="Count"/> is the number of print jobs cancelled
/// (only set by clearing jobs).</summary>
public sealed record PrinterActionOutcome(PrinterActionResult Result, int Count = 0)
{
    public bool IsDone => Result == PrinterActionResult.Done;

    public static PrinterActionOutcome Of(PrinterActionResult result) => new(result);
}
