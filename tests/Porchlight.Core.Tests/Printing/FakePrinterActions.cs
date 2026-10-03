using Porchlight.Core.Printing;

namespace Porchlight.Core.Tests.Printing;

/// <summary>In-memory <see cref="IPrinterActions"/> that records calls; never touches a real queue or spooler.</summary>
internal sealed class FakePrinterActions : IPrinterActions
{
    public SpoolerState SpoolerState { get; set; } = SpoolerState.Running;

    public PrinterActionOutcome StartResult { get; set; } = PrinterActionOutcome.Of(PrinterActionResult.Done);

    public PrinterActionOutcome ClearResult { get; set; } = new(PrinterActionResult.Done, 0);

    public PrinterActionOutcome RestartResult { get; set; } = PrinterActionOutcome.Of(PrinterActionResult.Done);

    public PrinterActionOutcome DefaultResult { get; set; } = PrinterActionOutcome.Of(PrinterActionResult.Done);

    public PrinterActionOutcome OnlineResult { get; set; } = PrinterActionOutcome.Of(PrinterActionResult.Done);

    public PrinterActionOutcome TestPageResult { get; set; } = PrinterActionOutcome.Of(PrinterActionResult.Done);

    public List<string> Calls { get; } = [];

    public bool? LastClearSpoolFiles { get; private set; }

    public Task<PrinterActionOutcome> SetDefaultAsync(string printerName, CancellationToken cancellationToken)
    {
        Calls.Add($"default:{printerName}");
        return Task.FromResult(DefaultResult);
    }

    public Task<PrinterActionOutcome> UseOnlineAsync(string printerName, CancellationToken cancellationToken)
    {
        Calls.Add($"online:{printerName}");
        return Task.FromResult(OnlineResult);
    }

    public Task<PrinterActionOutcome> PrintTestPageAsync(string printerName, CancellationToken cancellationToken)
    {
        Calls.Add($"test:{printerName}");
        return Task.FromResult(TestPageResult);
    }

    public Task<PrinterActionOutcome> ClearJobsAsync(string printerName, CancellationToken cancellationToken)
    {
        Calls.Add($"clear:{printerName}");
        return Task.FromResult(ClearResult);
    }

    public Task<SpoolerState> GetSpoolerStateAsync(CancellationToken cancellationToken)
    {
        Calls.Add("state");
        return Task.FromResult(SpoolerState);
    }

    public Task<PrinterActionOutcome> StartSpoolerAsync(CancellationToken cancellationToken)
    {
        Calls.Add("start");
        return Task.FromResult(StartResult);
    }

    public Task<PrinterActionOutcome> RestartSpoolerAsync(bool clearSpoolFiles, CancellationToken cancellationToken)
    {
        Calls.Add("restart");
        LastClearSpoolFiles = clearSpoolFiles;
        return Task.FromResult(RestartResult);
    }
}
