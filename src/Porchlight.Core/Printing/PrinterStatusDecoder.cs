namespace Porchlight.Core.Printing;

/// <summary>Turns the three numeric status fields of <c>Win32_Printer</c> into one plain state.
/// Pure, so the (messy) WMI codes are unit-tested.</summary>
public static class PrinterStatusDecoder
{
    // Win32_Printer.DetectedErrorState.
    private const int ErrorNoPaper = 4;
    private const int ErrorDoorOpen = 7;
    private const int ErrorJammed = 8;
    private const int ErrorOffline = 9;
    private const int ErrorServiceRequested = 10;
    private const int ErrorOutputBinFull = 11;

    // Win32_Printer.PrinterStatus.
    private const int StatusPrinting = 4;
    private const int StatusStoppedPrinting = 6;
    private const int StatusOffline = 7;

    // Win32_Printer.ExtendedPrinterStatus (superset of PrinterStatus).
    private const int ExtendedPrinting = 4;
    private const int ExtendedOffline = 7;
    private const int ExtendedPaused = 8;
    private const int ExtendedError = 9;
    private const int ExtendedNotAvailable = 11;
    private const int ExtendedIoActive = 17;

    public static PrinterStatusKind Decode(
        int? printerStatus, int? detectedErrorState, int? extendedPrinterStatus, bool workOffline)
    {
        // Specific hardware problems first: they tell the person what to do.
        if (detectedErrorState == ErrorJammed)
        {
            return PrinterStatusKind.PaperJam;
        }

        if (detectedErrorState == ErrorNoPaper)
        {
            return PrinterStatusKind.OutOfPaper;
        }

        if (workOffline
            || detectedErrorState == ErrorOffline
            || printerStatus == StatusOffline
            || extendedPrinterStatus is ExtendedOffline or ExtendedNotAvailable)
        {
            return PrinterStatusKind.Offline;
        }

        if (extendedPrinterStatus == ExtendedPaused)
        {
            return PrinterStatusKind.Paused;
        }

        if (extendedPrinterStatus == ExtendedError
            || printerStatus == StatusStoppedPrinting
            || detectedErrorState is ErrorDoorOpen or ErrorServiceRequested or ErrorOutputBinFull)
        {
            return PrinterStatusKind.Error;
        }

        if (printerStatus == StatusPrinting || extendedPrinterStatus is ExtendedPrinting or ExtendedIoActive)
        {
            return PrinterStatusKind.Printing;
        }

        return PrinterStatusKind.Ready;
    }

    public static string Label(PrinterStatusKind kind) => kind switch
    {
        PrinterStatusKind.Ready => "Ready",
        PrinterStatusKind.Printing => "Printing",
        PrinterStatusKind.Offline => "Offline",
        PrinterStatusKind.OutOfPaper => "Out of paper",
        PrinterStatusKind.PaperJam => "Paper jam",
        PrinterStatusKind.Paused => "Paused",
        _ => "Error",
    };
}
