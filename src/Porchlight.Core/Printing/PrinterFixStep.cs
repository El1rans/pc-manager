namespace Porchlight.Core.Printing;

/// <summary>One finished step of the guided fix with its plain-words outcome.</summary>
public sealed record PrinterFixStep(PrinterFixStepKind Kind, PrinterFixStatus Status, string Message);
