namespace Porchlight.Core.Printing;

/// <summary>A printer ready to show: decoded status, virtual-or-real, jobs waiting.</summary>
public sealed record PrinterEntry(
    string Name,
    bool IsDefault,
    bool WorkOffline,
    PrinterStatusKind Status,
    string? PortName,
    bool IsNetwork,
    int JobCount,
    bool IsVirtual);
