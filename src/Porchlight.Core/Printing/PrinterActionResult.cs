namespace Porchlight.Core.Printing;

public enum PrinterActionResult
{
    Done,
    NeedsAdmin,
    NotFound,
    NotSupported,
    TimedOut,
    Failed,
}
