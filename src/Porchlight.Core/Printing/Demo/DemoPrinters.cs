#if DEBUG
namespace Porchlight.Core.Printing.Demo;

/// <summary>
/// DEBUG-only fake <see cref="IPrinterService"/> and <see cref="IPrinterActions"/> for the "demo
/// data" mode (see <c>Monitoring.Demo.DemoDataMode</c>): a made-up printer list held in memory, so
/// screenshots never show the real machine's printers and nothing real is ever changed.
/// </summary>
internal sealed class DemoPrinters : IPrinterService, IPrinterActions
{
    private readonly object _gate = new();

    private readonly List<PrinterEntry> _printers =
    [
        new("Brother HL-L2350DW", true, false, PrinterStatusKind.Ready, "IP_192.168.1.40", true, 0, false),
        new("HP DeskJet 2700", false, true, PrinterStatusKind.Offline, "USB001", false, 2, false),
        new("Microsoft Print to PDF", false, false, PrinterStatusKind.Ready, "PORTPROMPT:", false, 0, true),
        new("OneNote (Desktop)", false, false, PrinterStatusKind.Ready, "nul:", false, 0, true),
    ];

    public Task<IReadOnlyList<PrinterEntry>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PrinterEntry>>([.. _printers]);
        }
    }

    public Task<PrinterActionOutcome> SetDefaultAsync(string printerName, CancellationToken cancellationToken) =>
        Change(printerName, p => p with { IsDefault = true }, others: p => p with { IsDefault = false });

    public Task<PrinterActionOutcome> UseOnlineAsync(string printerName, CancellationToken cancellationToken) =>
        Change(printerName, p => p with { WorkOffline = false, Status = PrinterStatusKind.Ready });

    public Task<PrinterActionOutcome> PrintTestPageAsync(string printerName, CancellationToken cancellationToken) =>
        Task.FromResult(PrinterActionOutcome.Of(PrinterActionResult.Done));

    public Task<PrinterActionOutcome> ClearJobsAsync(string printerName, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var index = _printers.FindIndex(p => p.Name == printerName);
            if (index < 0)
            {
                return Task.FromResult(PrinterActionOutcome.Of(PrinterActionResult.NotFound));
            }

            var count = _printers[index].JobCount;
            _printers[index] = _printers[index] with { JobCount = 0 };
            return Task.FromResult(new PrinterActionOutcome(PrinterActionResult.Done, count));
        }
    }

    public Task<SpoolerState> GetSpoolerStateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(SpoolerState.Running);

    public Task<PrinterActionOutcome> StartSpoolerAsync(CancellationToken cancellationToken) =>
        Task.FromResult(PrinterActionOutcome.Of(PrinterActionResult.Done));

    public Task<PrinterActionOutcome> RestartSpoolerAsync(bool clearSpoolFiles, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            for (var i = 0; i < _printers.Count; i++)
            {
                _printers[i] = _printers[i] with { JobCount = 0 };
            }
        }

        return Task.FromResult(PrinterActionOutcome.Of(PrinterActionResult.Done));
    }

    private Task<PrinterActionOutcome> Change(
        string printerName, Func<PrinterEntry, PrinterEntry> change, Func<PrinterEntry, PrinterEntry>? others = null)
    {
        lock (_gate)
        {
            if (!_printers.Exists(p => p.Name == printerName))
            {
                return Task.FromResult(PrinterActionOutcome.Of(PrinterActionResult.NotFound));
            }

            for (var i = 0; i < _printers.Count; i++)
            {
                if (_printers[i].Name == printerName)
                {
                    _printers[i] = change(_printers[i]);
                }
                else if (others is not null)
                {
                    _printers[i] = others(_printers[i]);
                }
            }
        }

        return Task.FromResult(PrinterActionOutcome.Of(PrinterActionResult.Done));
    }
}
#endif
