namespace Porchlight.Core.Printing;

/// <summary>One <c>Win32_Printer</c> row plus the number of jobs waiting for it, as read from WMI.</summary>
public sealed record PrinterRawInfo(
    string Name,
    bool IsDefault,
    bool WorkOffline,
    int? PrinterStatus,
    int? DetectedErrorState,
    int? ExtendedPrinterStatus,
    string? PortName,
    bool IsNetwork,
    int JobCount);
