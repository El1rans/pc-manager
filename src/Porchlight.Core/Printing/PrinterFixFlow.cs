namespace Porchlight.Core.Printing;

/// <summary>The guided "Fix my printer" run: check the print service is running, clear the default
/// printer's stuck jobs, then restart the print service (also removing leftover job files when jobs
/// were stuck). Each step's outcome is reported in plain words; a step that can't run stops the rest
/// from running only when later steps would be pointless. See docs/specs/36-printer-fixes.md.</summary>
public sealed class PrinterFixFlow
{
    private readonly IPrinterActions _actions;

    public PrinterFixFlow(IPrinterActions actions)
    {
        _actions = actions;
    }

    public async Task<PrinterFixReport> RunAsync(
        string? defaultPrinter, IProgress<PrinterFixStep>? progress, CancellationToken cancellationToken)
    {
        var steps = new List<PrinterFixStep>();

        void Add(PrinterFixStepKind kind, PrinterFixStatus status, string message)
        {
            var step = new PrinterFixStep(kind, status, message);
            steps.Add(step);
            progress?.Report(step);
        }

        // 1. The print service has to be running for anything else to work.
        var serviceOk = await CheckServiceAsync(Add, cancellationToken);
        if (!serviceOk)
        {
            const string blocked = "Skipped: the print service isn't running.";
            Add(PrinterFixStepKind.ClearJobs, PrinterFixStatus.Skipped, blocked);
            Add(PrinterFixStepKind.RestartService, PrinterFixStatus.Skipped, blocked);
            return new PrinterFixReport(steps);
        }

        // 2. Stuck jobs of the default printer.
        var needsFileCleanup = await ClearJobsAsync(defaultPrinter, Add, cancellationToken);

        // 3. A fresh start of the service, dropping leftover job files if jobs were in the way.
        var restart = await _actions.RestartSpoolerAsync(needsFileCleanup, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        switch (restart.Result)
        {
            case PrinterActionResult.Done:
                Add(
                    PrinterFixStepKind.RestartService,
                    PrinterFixStatus.Fixed,
                    needsFileCleanup
                        ? "Restarted the print service and removed leftover print files."
                        : "Restarted the print service.");
                break;
            case PrinterActionResult.NeedsAdmin:
                Add(PrinterFixStepKind.RestartService, PrinterFixStatus.NeedsAdmin, "Restarting the print service needs administrator rights.");
                break;
            case PrinterActionResult.TimedOut:
                Add(PrinterFixStepKind.RestartService, PrinterFixStatus.Failed, "The print service didn't respond in time.");
                break;
            default:
                Add(PrinterFixStepKind.RestartService, PrinterFixStatus.Failed, "Couldn't restart the print service.");
                break;
        }

        return new PrinterFixReport(steps);
    }

    private async Task<bool> CheckServiceAsync(
        Action<PrinterFixStepKind, PrinterFixStatus, string> add, CancellationToken cancellationToken)
    {
        var state = await _actions.GetSpoolerStateAsync(cancellationToken);
        if (state == SpoolerState.Running)
        {
            add(PrinterFixStepKind.CheckService, PrinterFixStatus.Fine, "The print service is running.");
            return true;
        }

        var start = await _actions.StartSpoolerAsync(cancellationToken);
        switch (start.Result)
        {
            case PrinterActionResult.Done:
                add(PrinterFixStepKind.CheckService, PrinterFixStatus.Fixed, "The print service was stopped. Started it again.");
                return true;
            case PrinterActionResult.NeedsAdmin:
                add(PrinterFixStepKind.CheckService, PrinterFixStatus.NeedsAdmin, "The print service is stopped, and starting it needs administrator rights.");
                return false;
            case PrinterActionResult.TimedOut:
                add(PrinterFixStepKind.CheckService, PrinterFixStatus.Failed, "The print service didn't start in time.");
                return false;
            default:
                add(PrinterFixStepKind.CheckService, PrinterFixStatus.Failed, "The print service is stopped and couldn't be started.");
                return false;
        }
    }

    /// <summary>Returns true when stuck jobs were found or couldn't be cleared, so the restart
    /// should also remove leftover job files.</summary>
    private async Task<bool> ClearJobsAsync(
        string? defaultPrinter, Action<PrinterFixStepKind, PrinterFixStatus, string> add, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(defaultPrinter))
        {
            add(PrinterFixStepKind.ClearJobs, PrinterFixStatus.Skipped, "No default printer is set, so there are no jobs to clear.");
            return false;
        }

        var cleared = await _actions.ClearJobsAsync(defaultPrinter, cancellationToken);
        switch (cleared.Result)
        {
            case PrinterActionResult.Done when cleared.Count == 0:
                add(PrinterFixStepKind.ClearJobs, PrinterFixStatus.Fine, $"No jobs were waiting for {defaultPrinter}.");
                return false;
            case PrinterActionResult.Done:
                var noun = cleared.Count == 1 ? "job" : "jobs";
                add(PrinterFixStepKind.ClearJobs, PrinterFixStatus.Fixed, $"Cancelled {cleared.Count} stuck {noun} for {defaultPrinter}.");
                return true;
            case PrinterActionResult.NeedsAdmin:
                add(PrinterFixStepKind.ClearJobs, PrinterFixStatus.NeedsAdmin, "Clearing these jobs needs administrator rights.");
                return true;
            default:
                add(PrinterFixStepKind.ClearJobs, PrinterFixStatus.Failed, $"Couldn't clear all jobs for {defaultPrinter}.");
                return true;
        }
    }
}
