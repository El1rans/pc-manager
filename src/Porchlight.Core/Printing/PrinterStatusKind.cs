namespace Porchlight.Core.Printing;

/// <summary>A printer's state in the plain words the page uses.</summary>
public enum PrinterStatusKind
{
    Ready,
    Printing,
    Offline,
    OutOfPaper,
    PaperJam,
    Paused,
    Error,
}
