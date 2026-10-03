namespace Porchlight.Core.Printing;

/// <summary>Everything that changes printers, jobs or the print service. Behind an interface so
/// tests never purge a real queue or restart the real spooler.</summary>
public interface IPrinterActions
{
    Task<PrinterActionOutcome> SetDefaultAsync(string printerName, CancellationToken cancellationToken);

    /// <summary>Clears the "Use printer offline" setting.</summary>
    Task<PrinterActionOutcome> UseOnlineAsync(string printerName, CancellationToken cancellationToken);

    Task<PrinterActionOutcome> PrintTestPageAsync(string printerName, CancellationToken cancellationToken);

    /// <summary>Cancels every waiting job of the printer; <see cref="PrinterActionOutcome.Count"/> says how many.</summary>
    Task<PrinterActionOutcome> ClearJobsAsync(string printerName, CancellationToken cancellationToken);

    Task<SpoolerState> GetSpoolerStateAsync(CancellationToken cancellationToken);

    Task<PrinterActionOutcome> StartSpoolerAsync(CancellationToken cancellationToken);

    /// <summary>Stops the print service, optionally deletes its queued-job files, and starts it again
    /// (it is always started again, even if deleting failed).</summary>
    Task<PrinterActionOutcome> RestartSpoolerAsync(bool clearSpoolFiles, CancellationToken cancellationToken);
}
