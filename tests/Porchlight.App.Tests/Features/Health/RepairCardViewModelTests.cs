using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Health;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Health;
using Xunit;

namespace Porchlight.App.Tests.Features.Health;

public sealed class RepairCardViewModelTests
{
    private readonly FakeRepairService _service = new();
    private readonly FakeElevationService _elevation = new(isElevated: true);

    private RepairCardViewModel CreateViewModel() =>
        new(_service, _elevation, NullLogger<RepairCardViewModel>.Instance);

    /// <summary>Starts a command with a synchronous <see cref="SynchronizationContext"/> installed, so the
    /// view model's <c>Progress&lt;T&gt;</c> (which captures the context current when the run starts)
    /// delivers each report immediately instead of on a thread-pool thread.</summary>
    private static Task Run(IAsyncRelayCommand command)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
        try
        {
            return command.ExecuteAsync(null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private async Task<RepairCardViewModel> CreateWithDeeperRepairOfferedAsync()
    {
        _service.SfcOutcome = SfcOutcome.CouldNotRepair;
        var viewModel = CreateViewModel();
        await Run(viewModel.StartCheckCommand);
        return viewModel;
    }

    [Fact]
    public void WithoutAdministratorRights_NeitherCommandCanRun()
    {
        _elevation.IsElevated = false;
        var viewModel = CreateViewModel();

        Assert.False(viewModel.StartCheckCommand.CanExecute(null));
        Assert.False(viewModel.RunDeeperRepairCommand.CanExecute(null));
    }

    [Fact]
    public void AsAdministrator_OnlyTheCheckCanRunAtFirst()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.StartCheckCommand.CanExecute(null));
        Assert.False(viewModel.RunDeeperRepairCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(SfcOutcome.NoProblems, HealthSeverity.Ok, false)]
    [InlineData(SfcOutcome.Repaired, HealthSeverity.Ok, false)]
    [InlineData(SfcOutcome.Unknown, HealthSeverity.Neutral, false)]
    [InlineData(SfcOutcome.RebootPending, HealthSeverity.Warning, false)]
    [InlineData(SfcOutcome.CouldNotRun, HealthSeverity.Warning, false)]
    [InlineData(SfcOutcome.CouldNotRepair, HealthSeverity.Warning, true)]
    public async Task Check_ShowsTheOutcome_AndOffersTheDeeperRepairOnlyWhenSfcCouldNotFixIt(
        SfcOutcome outcome, HealthSeverity severity, bool deeperOffered)
    {
        _service.SfcOutcome = outcome;
        var viewModel = CreateViewModel();

        await Run(viewModel.StartCheckCommand);

        Assert.Equal(RepairOutcomeDescriber.Describe(outcome), viewModel.ResultText);
        Assert.True(viewModel.HasResult);
        Assert.Equal(severity, viewModel.ResultSeverity);
        Assert.Equal(deeperOffered, viewModel.DeeperRepairOffered);
        Assert.Equal(deeperOffered, viewModel.RunDeeperRepairCommand.CanExecute(null));
    }

    [Fact]
    public async Task WhileChecking_ShowsProgressAndBlocksBothCommands_ThenResetsWhenDone()
    {
        var gate = new TaskCompletionSource<SfcOutcome>();
        _service.SfcGate = gate;
        var viewModel = CreateViewModel();

        var run = Run(viewModel.StartCheckCommand);

        Assert.True(viewModel.IsRunning);
        Assert.True(viewModel.IsIndeterminate);
        Assert.Equal("Checking Windows files...", viewModel.StepText);
        Assert.False(viewModel.StartCheckCommand.CanExecute(null));

        gate.SetResult(SfcOutcome.NoProblems);
        await run;

        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.IsIndeterminate);
        Assert.Null(viewModel.StepText);
        Assert.True(viewModel.StartCheckCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartingAgain_ClearsThePreviousResultAndOffer()
    {
        var viewModel = await CreateWithDeeperRepairOfferedAsync();
        var gate = new TaskCompletionSource<SfcOutcome>();
        _service.SfcGate = gate;

        var run = Run(viewModel.StartCheckCommand);

        Assert.False(viewModel.HasResult);
        Assert.False(viewModel.DeeperRepairOffered);

        gate.SetResult(SfcOutcome.NoProblems);
        await run;
    }

    [Fact]
    public async Task ProgressReports_UpdatePercentAndLog_AndALineOnlyReportKeepsThePercent()
    {
        _service.SfcProgress = [new RepairProgress(40, "Scanning"), new RepairProgress(null, "Verifying")];
        var viewModel = CreateViewModel();

        await Run(viewModel.StartCheckCommand);

        Assert.Equal(40, viewModel.Percent);
        Assert.Equal($"Scanning{Environment.NewLine}Verifying{Environment.NewLine}", viewModel.LogText);
    }

    [Fact]
    public async Task ProgressWithPercent_SwitchesOffTheIndeterminateBar()
    {
        var gate = new TaskCompletionSource<SfcOutcome>();
        _service.SfcGate = gate;
        _service.SfcProgress = [new RepairProgress(10, null)];
        var viewModel = CreateViewModel();

        var run = Run(viewModel.StartCheckCommand);

        Assert.False(viewModel.IsIndeterminate);
        Assert.Equal(10, viewModel.Percent);

        gate.SetResult(SfcOutcome.NoProblems);
        await run;
    }

    [Fact]
    public async Task VeryLongOutput_IsTrimmedFromTheStart_KeepingTheNewestLines()
    {
        var longLine = new string('x', 1000);
        _service.SfcProgress =
        [
            .. Enumerable.Range(0, 150).Select(_ => new RepairProgress(null, longLine)),
            new RepairProgress(null, "last line"),
        ];
        var viewModel = CreateViewModel();

        await Run(viewModel.StartCheckCommand);

        Assert.InRange(viewModel.LogText.Length, 1, 100_000);
        Assert.EndsWith($"last line{Environment.NewLine}", viewModel.LogText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckThrowing_ShowsAWarning_AndStopsRunning()
    {
        _service.SfcFailure = new InvalidOperationException("boom");
        var viewModel = CreateViewModel();

        await Run(viewModel.StartCheckCommand);

        Assert.Equal("The check stopped unexpectedly. Restart your PC and try again.", viewModel.ResultText);
        Assert.Equal(HealthSeverity.Warning, viewModel.ResultSeverity);
        Assert.False(viewModel.IsRunning);
    }

    [Theory]
    [InlineData(DismOutcome.Succeeded, HealthSeverity.Ok)]
    [InlineData(DismOutcome.SourceNotFound, HealthSeverity.Warning)]
    [InlineData(DismOutcome.NeedsAdmin, HealthSeverity.Warning)]
    [InlineData(DismOutcome.Failed, HealthSeverity.Warning)]
    public async Task DeeperRepair_ShowsTheOutcome_AndWithdrawsTheOffer(DismOutcome outcome, HealthSeverity severity)
    {
        var viewModel = await CreateWithDeeperRepairOfferedAsync();
        _service.DismOutcome = outcome;

        await Run(viewModel.RunDeeperRepairCommand);

        Assert.Equal(RepairOutcomeDescriber.Describe(outcome), viewModel.ResultText);
        Assert.Equal(severity, viewModel.ResultSeverity);
        Assert.False(viewModel.DeeperRepairOffered);
        Assert.False(viewModel.RunDeeperRepairCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeeperRepairThrowing_ShowsAWarning_AndStopsRunning()
    {
        var viewModel = await CreateWithDeeperRepairOfferedAsync();
        _service.DismFailure = new InvalidOperationException("boom");

        await Run(viewModel.RunDeeperRepairCommand);

        Assert.Equal("The deeper repair stopped unexpectedly. Restart your PC and try again.", viewModel.ResultText);
        Assert.Equal(HealthSeverity.Warning, viewModel.ResultSeverity);
        Assert.False(viewModel.IsRunning);
    }

    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
    }

    private sealed class FakeRepairService : IWindowsRepairService
    {
        public SfcOutcome SfcOutcome { get; set; } = SfcOutcome.NoProblems;

        public DismOutcome DismOutcome { get; set; } = DismOutcome.Succeeded;

        public TaskCompletionSource<SfcOutcome>? SfcGate { get; set; }

        public IReadOnlyList<RepairProgress> SfcProgress { get; set; } = [];

        public Exception? SfcFailure { get; set; }

        public Exception? DismFailure { get; set; }

        public async Task<SfcOutcome> RunSfcAsync(IProgress<RepairProgress>? progress, CancellationToken cancellationToken)
        {
            foreach (var update in SfcProgress)
            {
                progress?.Report(update);
            }

            if (SfcFailure is not null)
            {
                throw SfcFailure;
            }

            return SfcGate is { } gate ? await gate.Task : SfcOutcome;
        }

        public Task<DismOutcome> RunDismAsync(IProgress<RepairProgress>? progress, CancellationToken cancellationToken) =>
            DismFailure is null ? Task.FromResult(DismOutcome) : throw DismFailure;
    }
}
