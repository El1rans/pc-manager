using Porchlight.Core.Printing;
using Xunit;

namespace Porchlight.Core.Tests.Printing;

public sealed class PrinterFixFlowTests
{
    private const string Printer = "Brother HL-L2350DW";

    private readonly FakePrinterActions _actions = new();

    private Task<PrinterFixReport> RunAsync(string? printer = Printer, IProgress<PrinterFixStep>? progress = null) =>
        new PrinterFixFlow(_actions).RunAsync(printer, progress, TestContext.Current.CancellationToken);

    [Fact]
    public async Task HealthyPc_ChecksClearsAndRestartsInOrder()
    {
        var report = await RunAsync();

        Assert.Equal(["state", $"clear:{Printer}", "restart"], _actions.Calls);
        Assert.Equal(
            [PrinterFixStepKind.CheckService, PrinterFixStepKind.ClearJobs, PrinterFixStepKind.RestartService],
            report.Steps.Select(s => s.Kind));
        Assert.Equal(PrinterFixStatus.Fine, report.Steps[0].Status);
        Assert.Equal(PrinterFixStatus.Fine, report.Steps[1].Status);
        Assert.Equal(PrinterFixStatus.Fixed, report.Steps[2].Status);
        Assert.False(report.HasProblem);
        Assert.Equal("Done. Try printing again.", report.Summary);
        Assert.False(_actions.LastClearSpoolFiles);
    }

    [Fact]
    public async Task StoppedSpooler_IsStartedFirst()
    {
        _actions.SpoolerState = SpoolerState.Stopped;

        var report = await RunAsync();

        Assert.Equal(["state", "start", $"clear:{Printer}", "restart"], _actions.Calls);
        Assert.Equal(PrinterFixStatus.Fixed, report.Steps[0].Status);
        Assert.Contains("stopped", report.Steps[0].Message);
    }

    [Theory]
    [InlineData(PrinterActionResult.NeedsAdmin, PrinterFixStatus.NeedsAdmin)]
    [InlineData(PrinterActionResult.TimedOut, PrinterFixStatus.Failed)]
    [InlineData(PrinterActionResult.Failed, PrinterFixStatus.Failed)]
    public async Task SpoolerThatWontStart_StopsTheRest(PrinterActionResult startResult, PrinterFixStatus expected)
    {
        _actions.SpoolerState = SpoolerState.Stopped;
        _actions.StartResult = PrinterActionOutcome.Of(startResult);

        var report = await RunAsync();

        Assert.Equal(["state", "start"], _actions.Calls);
        Assert.Equal(expected, report.Steps[0].Status);
        Assert.Equal(PrinterFixStatus.Skipped, report.Steps[1].Status);
        Assert.Equal(PrinterFixStatus.Skipped, report.Steps[2].Status);
        Assert.True(report.HasProblem);
    }

    [Fact]
    public async Task StuckJobs_AreCancelledAndLeftoverFilesRemovedOnRestart()
    {
        _actions.ClearResult = new PrinterActionOutcome(PrinterActionResult.Done, 3);

        var report = await RunAsync();

        Assert.Equal(PrinterFixStatus.Fixed, report.Steps[1].Status);
        Assert.Equal($"Cancelled 3 stuck jobs for {Printer}.", report.Steps[1].Message);
        Assert.True(_actions.LastClearSpoolFiles);
        Assert.Contains("leftover", report.Steps[2].Message);
    }

    [Fact]
    public async Task SingleStuckJob_UsesSingularWord()
    {
        _actions.ClearResult = new PrinterActionOutcome(PrinterActionResult.Done, 1);

        var report = await RunAsync();

        Assert.Equal($"Cancelled 1 stuck job for {Printer}.", report.Steps[1].Message);
    }

    [Fact]
    public async Task NoDefaultPrinter_SkipsClearingButStillRestarts()
    {
        var report = await RunAsync(printer: null);

        Assert.Equal(["state", "restart"], _actions.Calls);
        Assert.Equal(PrinterFixStatus.Skipped, report.Steps[1].Status);
        Assert.False(report.HasProblem);
    }

    [Fact]
    public async Task ClearFailure_IsReportedAndRestartStillRemovesFiles()
    {
        _actions.ClearResult = new PrinterActionOutcome(PrinterActionResult.Failed, 1);

        var report = await RunAsync();

        Assert.Equal(PrinterFixStatus.Failed, report.Steps[1].Status);
        Assert.Equal("restart", _actions.Calls[^1]);
        Assert.True(_actions.LastClearSpoolFiles);
        Assert.True(report.HasProblem);
    }

    [Fact]
    public async Task RestartNeedsAdmin_SummaryTellsToRestartAsAdministrator()
    {
        _actions.RestartResult = PrinterActionOutcome.Of(PrinterActionResult.NeedsAdmin);

        var report = await RunAsync();

        Assert.Equal(PrinterFixStatus.NeedsAdmin, report.Steps[2].Status);
        Assert.True(report.NeedsAdmin);
        Assert.Contains("administrator", report.Summary);
    }

    [Fact]
    public async Task RestartTimeout_IsAFailure()
    {
        _actions.RestartResult = PrinterActionOutcome.Of(PrinterActionResult.TimedOut);

        var report = await RunAsync();

        Assert.Equal(PrinterFixStatus.Failed, report.Steps[2].Status);
        Assert.Contains("Not everything", report.Summary);
    }

    [Fact]
    public async Task Progress_ReportsEachStepAsItFinishes()
    {
        var seen = new List<PrinterFixStepKind>();

        var report = await RunAsync(progress: new SyncProgress(step => seen.Add(step.Kind)));

        Assert.Equal(report.Steps.Select(s => s.Kind), seen);
    }

    [Fact]
    public async Task Cancellation_StopsTheFlow()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var actions = new CancellingActions();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new PrinterFixFlow(actions).RunAsync(Printer, null, cts.Token));
    }

    private sealed class SyncProgress : IProgress<PrinterFixStep>
    {
        private readonly Action<PrinterFixStep> _handler;

        public SyncProgress(Action<PrinterFixStep> handler)
        {
            _handler = handler;
        }

        public void Report(PrinterFixStep value) => _handler(value);
    }

    private sealed class CancellingActions : IPrinterActions
    {
        public Task<PrinterActionOutcome> SetDefaultAsync(string printerName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PrinterActionOutcome> UseOnlineAsync(string printerName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PrinterActionOutcome> PrintTestPageAsync(string printerName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PrinterActionOutcome> ClearJobsAsync(string printerName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SpoolerState> GetSpoolerStateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SpoolerState.Running);
        }

        public Task<PrinterActionOutcome> StartSpoolerAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PrinterActionOutcome> RestartSpoolerAsync(bool clearSpoolFiles, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
