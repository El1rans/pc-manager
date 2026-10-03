using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Features.Health;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Backup;
using Porchlight.Core.Health;
using Porchlight.Core.Processes;
using Xunit;

namespace Porchlight.App.Tests.Features.Health;

public sealed class BackupCardViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeService : IBackupStatusService
    {
        public HealthReadResult<BackupSnapshot> Result { get; set; } = HealthReadResult<BackupSnapshot>.Ok(
            new BackupSnapshot(FileHistoryStatus.NotSetUp, OneDriveStatus.NotInstalled, []));

        public Task<HealthReadResult<BackupSnapshot>> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }

    private sealed class ThrowingProcessRunner : IProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(
            string fileName, IReadOnlyList<string> arguments, IProgress<string>? onLine, IProgress<string>? onProgress,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public void StartDetached(string fileName, IReadOnlyList<string> arguments) =>
            throw new Win32Exception("no");
    }

    private readonly FakeService _service = new();
    private readonly FakeProcessRunner _runner = new();

    private BackupCardViewModel Create(IProcessRunner? runner = null) =>
        new(_service, runner ?? _runner, new FakeTimeProvider(Now), NullLogger<BackupCardViewModel>.Instance);

    [Fact]
    public async Task Nothing_ShowsProblemWithFileHistoryButton()
    {
        var viewModel = Create();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.True(viewModel.HasResult);
        Assert.Equal(HealthSeverity.Problem, viewModel.Severity);
        Assert.Equal("Nothing is backing up your files.", viewModel.HeadlineText);
        Assert.True(viewModel.CanTurnOnFileHistory);
        Assert.False(viewModel.CanOpenOneDrive);
        Assert.NotEmpty(viewModel.DetailLines);
        Assert.False(viewModel.IsChecking);
    }

    [Fact]
    public async Task Recent_ShowsOkAndNoNudge()
    {
        _service.Result = HealthReadResult<BackupSnapshot>.Ok(new BackupSnapshot(
            new FileHistoryStatus(true, true, Now.AddDays(-1)), OneDriveStatus.NotInstalled, []));
        var viewModel = Create();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(HealthSeverity.Ok, viewModel.Severity);
        Assert.Null(viewModel.NudgeText);
        Assert.False(viewModel.CanTurnOnFileHistory);
    }

    [Fact]
    public async Task Failure_ShowsTheReasonAndHidesTheResult()
    {
        var viewModel = Create();
        await viewModel.RefreshAsync(CancellationToken.None);
        _service.Result = HealthReadResult<BackupSnapshot>.Fail("Nope.");

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal("Couldn't check. Nope.", viewModel.ErrorText);
        Assert.False(viewModel.HasResult);
        Assert.Empty(viewModel.DetailLines);
        Assert.False(viewModel.CanTurnOnFileHistory);
    }

    [Fact]
    public async Task Refresh_AfterFailure_ClearsTheError()
    {
        _service.Result = HealthReadResult<BackupSnapshot>.Fail("Nope.");
        var viewModel = Create();
        await viewModel.RefreshAsync(CancellationToken.None);
        _service.Result = HealthReadResult<BackupSnapshot>.Ok(new BackupSnapshot(
            FileHistoryStatus.NotSetUp, OneDriveStatus.NotInstalled, ["Dropbox"]));

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.False(viewModel.ShowError);
        Assert.Contains("Dropbox", viewModel.OtherToolsText);
    }

    [Fact]
    public async Task Buttons_OpenWindowsScreens_AndNothingElse()
    {
        const string exe = @"C:\Users\Zelda\AppData\Local\Microsoft\OneDrive\OneDrive.exe";
        _service.Result = HealthReadResult<BackupSnapshot>.Ok(new BackupSnapshot(
            FileHistoryStatus.NotSetUp, new OneDriveStatus(true, false, false, false, false, exe), []));
        var viewModel = Create();
        await viewModel.RefreshAsync(CancellationToken.None);

        viewModel.TurnOnFileHistoryCommand.Execute(null);
        viewModel.OpenBackupSettingsCommand.Execute(null);
        viewModel.OpenOneDriveCommand.Execute(null);

        Assert.True(viewModel.CanOpenOneDrive);
        Assert.Equal(3, _runner.StartDetachedCalls.Count);
        Assert.EndsWith("control.exe", _runner.StartDetachedCalls[0].FileName);
        Assert.Equal(["/name", "Microsoft.FileHistory"], _runner.StartDetachedCalls[0].Arguments);
        Assert.EndsWith("explorer.exe", _runner.StartDetachedCalls[1].FileName);
        Assert.Equal(["ms-settings:backup"], _runner.StartDetachedCalls[1].Arguments);
        Assert.Equal(exe, _runner.StartDetachedCalls[2].FileName);
        Assert.Empty(_runner.RunCalls);
    }

    [Fact]
    public async Task LaunchFailure_ShowsAFriendlyMessage()
    {
        var viewModel = Create(new ThrowingProcessRunner());
        await viewModel.RefreshAsync(CancellationToken.None);

        viewModel.OpenBackupSettingsCommand.Execute(null);

        Assert.Contains("Couldn't open backup settings", viewModel.LaunchError);
    }
}
