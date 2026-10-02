using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Health;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Health;
using Porchlight.Core.Processes;
using Xunit;

namespace Porchlight.App.Tests.Features.Health;

public sealed class RestorePointCardViewModelTests
{
    private readonly FakeRestorePointService _service = new();
    private readonly FakeElevationService _elevation = new(isElevated: true);
    private readonly FakeProcessRunner _processRunner = new();

    private RestorePointCardViewModel CreateViewModel(IProcessRunner? processRunner = null) =>
        new(_service, _elevation, processRunner ?? _processRunner, NullLogger<RestorePointCardViewModel>.Instance);

    private static RestorePointInfo OldPoint(int sequence, string description) =>
        new(sequence, description, DateTimeOffset.Now.AddYears(-1));

    [Fact]
    public async Task Refresh_ReadFailing_ShowsTheReason_ClearsTheListAndBlocksCreate()
    {
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        _service.Status = HealthReadResult<RestorePointStatus>.Fail("Windows said no.");

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal("Couldn't check. Windows said no.", viewModel.ErrorText);
        Assert.Empty(viewModel.Recent);
        Assert.Null(viewModel.AvailabilityText);
        Assert.False(viewModel.CreateCommand.CanExecute(null));
        Assert.False(viewModel.IsChecking);
    }

    [Fact]
    public async Task Refresh_ShowsCheckingWhileReading()
    {
        var gate = new TaskCompletionSource<HealthReadResult<RestorePointStatus>>();
        _service.StatusGate = gate;
        var viewModel = CreateViewModel();

        var refresh = viewModel.RefreshAsync(CancellationToken.None);

        Assert.True(viewModel.IsChecking);

        gate.SetResult(_service.Status);
        await refresh;
        Assert.False(viewModel.IsChecking);
    }

    [Fact]
    public async Task Refresh_RestorePointAllowed_AsAdministrator_EnablesCreate()
    {
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Null(viewModel.ErrorText);
        Assert.NotNull(viewModel.AvailabilityText);
        Assert.True(viewModel.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task Refresh_WithoutAdministratorRights_KeepsCreateDisabled()
    {
        _elevation.IsElevated = false;
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.False(viewModel.CreateCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(null, false, true)]
    public async Task Refresh_ProtectionOff_IsFlagged_AndOnlyThenBlocksCreate(bool? protectionEnabled, bool protectionOff, bool canCreate)
    {
        _service.Status = HealthReadResult<RestorePointStatus>.Ok(new(protectionEnabled, 1440, [OldPoint(1, "Old")]));
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(protectionOff, viewModel.ProtectionOff);
        Assert.Equal(canCreate, viewModel.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task Refresh_ListsRecentPoints_WithoutANote()
    {
        _service.Status = HealthReadResult<RestorePointStatus>.Ok(
            new(true, 1440, [OldPoint(2, "Second"), OldPoint(1, "First")]));
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(["Second", "First"], viewModel.Recent.Select(r => r.Description));
        Assert.Null(viewModel.RecentNote);
    }

    [Theory]
    [MemberData(nameof(RecentNoteCases))]
    public async Task Refresh_ExplainsAnEmptyOrUnreadableList(IReadOnlyList<RestorePointInfo>? recent, string expectedNote)
    {
        _service.Status = HealthReadResult<RestorePointStatus>.Ok(new(true, 1440, recent));
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(expectedNote, viewModel.RecentNote);
        Assert.Empty(viewModel.Recent);
    }

    public static TheoryData<IReadOnlyList<RestorePointInfo>?, string> RecentNoteCases => new()
    {
        { [], "There are no restore points yet." },
        { null, "Couldn't read the list of restore points. This usually needs administrator rights." },
    };

    [Fact]
    public async Task Create_AsksWindowsForARestorePoint_ShowsItsMessage_AndRefreshes()
    {
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        _service.CreateResult = new RestorePointCreateResult(RestorePointCreateOutcome.Created, "Done.");

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Equal(("Porchlight restore point", RestorePointKind.ApplicationInstall), Assert.Single(_service.CreateCalls));
        Assert.Equal("Done.", viewModel.ResultText);
        Assert.True(viewModel.HasResult);
        Assert.Equal(2, _service.StatusCalls);
        Assert.False(viewModel.IsCreating);
    }

    [Fact]
    public async Task Create_WhileCreating_ShowsProgressAndBlocksCreate()
    {
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        var gate = new TaskCompletionSource<RestorePointCreateResult>();
        _service.CreateGate = gate;

        var create = viewModel.CreateCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsCreating);
        Assert.Equal("Creating a restore point. This can take a minute...", viewModel.ResultText);
        Assert.False(viewModel.CreateCommand.CanExecute(null));

        gate.SetResult(new RestorePointCreateResult(RestorePointCreateOutcome.Created, "Done."));
        await create;
    }

    [Fact]
    public void OpenSystemProtection_LaunchesTheWindowsDialog()
    {
        var viewModel = CreateViewModel();

        viewModel.OpenSystemProtectionCommand.Execute(null);

        var (fileName, _) = Assert.Single(_processRunner.StartDetachedCalls);
        Assert.EndsWith("SystemPropertiesProtection.exe", fileName, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(LaunchFailures))]
    public void OpenSystemProtection_LaunchFailing_TellsTheUserWhereElseToLook(Exception failure)
    {
        var viewModel = CreateViewModel(new ThrowingProcessRunner(failure));

        viewModel.OpenSystemProtectionCommand.Execute(null);

        Assert.Contains("Create a restore point", viewModel.ResultText, StringComparison.Ordinal);
    }

    public static TheoryData<Exception> LaunchFailures => new()
    {
        new Win32Exception("not found"),
        new InvalidOperationException("no shell"),
    };

    private sealed class FakeRestorePointService : IRestorePointService
    {
        public HealthReadResult<RestorePointStatus> Status { get; set; } =
            HealthReadResult<RestorePointStatus>.Ok(new(true, 1440, [OldPoint(1, "Old")]));

        public TaskCompletionSource<HealthReadResult<RestorePointStatus>>? StatusGate { get; set; }

        public RestorePointCreateResult CreateResult { get; set; } =
            new(RestorePointCreateOutcome.Created, "Created.");

        public TaskCompletionSource<RestorePointCreateResult>? CreateGate { get; set; }

        public int StatusCalls { get; private set; }

        public List<(string Description, RestorePointKind Kind)> CreateCalls { get; } = [];

        public Task<HealthReadResult<RestorePointStatus>> GetStatusAsync(CancellationToken cancellationToken)
        {
            StatusCalls++;
            return StatusGate?.Task ?? Task.FromResult(Status);
        }

        public Task<RestorePointCreateResult> CreateAsync(
            string description, RestorePointKind kind, CancellationToken cancellationToken)
        {
            CreateCalls.Add((description, kind));
            return CreateGate?.Task ?? Task.FromResult(CreateResult);
        }
    }

    private sealed class ThrowingProcessRunner(Exception failure) : IProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(
            string fileName, IReadOnlyList<string> arguments, IProgress<string>? onLine, IProgress<string>? onProgress,
            CancellationToken cancellationToken) => throw failure;

        public void StartDetached(string fileName, IReadOnlyList<string> arguments) => throw failure;
    }
}
