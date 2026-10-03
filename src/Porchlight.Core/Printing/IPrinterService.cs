namespace Porchlight.Core.Printing;

/// <summary>Lists the printers for the "Printers" page. See docs/specs/36-printer-fixes.md.</summary>
public interface IPrinterService
{
    /// <summary>Default printer first, then by name. Runs off the calling thread.</summary>
    Task<IReadOnlyList<PrinterEntry>> ListAsync(CancellationToken cancellationToken);
}
