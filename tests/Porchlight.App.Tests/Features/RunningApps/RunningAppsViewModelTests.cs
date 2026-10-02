using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Features.RunningApps;
using Porchlight.App.Shell;
using Porchlight.Core.Processes;
using Porchlight.Core.RunningApps;
using Xunit;

namespace Porchlight.App.Tests.Features.RunningApps;

public sealed class RunningAppsViewModelTests : IDisposable
{
    private const long Mb = 1024 * 1024;

    private readonly FakeRunningAppsService _service = new();
    private readonly FakeConfirmation _confirmation = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly FakeTimeProvider _time = new();

    public void Dispose() => _service.Dispose();

    private RunningAppsViewModel Create() =>
        new(_service, _confirmation, _runner, _time, NullLogger<RunningAppsViewModel>.Instance);

    private static RunningApp App(
        string key,
        string name,
        RunningAppSection section = RunningAppSection.Apps,
        double cpu = 0,
        long memory = 100 * Mb,
        int count = 1,
        bool canEnd = true,
        string? path = null) =>
        new(key, name, path, section, canEnd, count, cpu, memory);

    private static RunningAppsSnapshot Snapshot(params RunningApp[] apps) =>
        new(apps, 23, new MemoryStatus(16L * 1024 * Mb, 16L * 1024 * Mb - (long)(9.4 * 1024 * Mb)));

    private async Task<RunningAppsViewModel> LoadedAsync(params RunningApp[] apps)
    {
        _service.Snapshot = Snapshot(apps);
        var vm = Create();
        using var cts = new CancellationTokenSource();
        var task = vm.OnNavigatedToAsync(cts.Token);
        await _service.WaitForSamplesAsync(1);
        await cts.CancelAsync();
        await task;
        return vm;
    }

    [Fact]
    public async Task Load_PutsAppsInSections_WithCountsSummaryAndPageInfo()
    {
        var vm = await LoadedAsync(
            App("chrome", "Google Chrome", count: 18, cpu: 12.34, memory: 1536 * Mb),
            App("sync", "Cloud Sync", RunningAppSection.Background),
            App("svc", "Windows Shell", RunningAppSection.Windows, canEnd: false));

        Assert.Equal("Running apps", vm.Title);
        Assert.Equal(PageCategory.Apps, vm.Category);
        Assert.Equal(2, vm.Order);
        Assert.Equal("Google Chrome (18)", vm.Apps.Items.Single().DisplayName);
        Assert.Equal("12.3%", vm.Apps.Items.Single().CpuText);
        Assert.Equal("1.5 GB", vm.Apps.Items.Single().MemoryText);
        Assert.Single(vm.Background.Items);
        Assert.Single(vm.Windows.Items);
        Assert.False(vm.Windows.IsExpanded);
        Assert.True(vm.Apps.IsExpanded);
        Assert.Equal("Apps (1)", vm.Apps.HeaderText);
        Assert.Equal("CPU 23% · Memory 9.4 GB of 16 GB in use", vm.Summary);
        Assert.False(vm.Windows.Items.Single().CanEnd);
    }

    [Fact]
    public async Task Refresh_UpdatesRowsInPlace_KeepingInstances()
    {
        var vm = await LoadedAsync(App("a", "A", cpu: 1), App("b", "B", cpu: 5));
        var rowA = vm.Apps.Items.Single(r => r.Key == "a");
        var rowB = vm.Apps.Items.Single(r => r.Key == "b");
        Assert.Same(rowB, vm.Apps.Items[0]);

        _service.Snapshot = Snapshot(App("a", "A", cpu: 50, memory: 300 * Mb), App("b", "B", cpu: 5), App("c", "C", cpu: 0));
        using var cts = new CancellationTokenSource();
        var task = vm.OnNavigatedToAsync(cts.Token);
        await _service.WaitForSamplesAsync(2);
        await cts.CancelAsync();
        await task;

        Assert.Same(rowA, vm.Apps.Items[0]);
        Assert.Same(rowB, vm.Apps.Items[1]);
        Assert.Equal("50.0%", rowA.CpuText);
        Assert.Equal("300.0 MB", rowA.MemoryText);
        Assert.Equal(3, vm.Apps.Items.Count);
    }

    [Fact]
    public async Task Refresh_RemovesAppsThatAreGone_AndMovesRowsBetweenSections()
    {
        var vm = await LoadedAsync(App("a", "A"), App("b", "B"));
        var rowB = vm.Apps.Items.Single(r => r.Key == "b");

        _service.Snapshot = Snapshot(App("b", "B", RunningAppSection.Background));
        using var cts = new CancellationTokenSource();
        var task = vm.OnNavigatedToAsync(cts.Token);
        await _service.WaitForSamplesAsync(2);
        await cts.CancelAsync();
        await task;

        Assert.Empty(vm.Apps.Items);
        Assert.Same(rowB, vm.Background.Items.Single());
    }

    [Fact]
    public async Task Sort_ByCpuMemoryAndName()
    {
        var vm = await LoadedAsync(
            App("a", "Alpha", cpu: 1, memory: 900 * Mb),
            App("b", "Bravo", cpu: 9, memory: 100 * Mb),
            App("c", "Charlie", cpu: 5, memory: 500 * Mb));

        Assert.Equal(["Bravo", "Charlie", "Alpha"], vm.Apps.Items.Select(r => r.Name));

        vm.SelectedSort = RunningAppsSort.Memory;
        Assert.Equal(["Alpha", "Charlie", "Bravo"], vm.Apps.Items.Select(r => r.Name));

        vm.SelectedSort = RunningAppsSort.Name;
        Assert.Equal(["Alpha", "Bravo", "Charlie"], vm.Apps.Items.Select(r => r.Name));
    }

    [Fact]
    public async Task Filter_HidesNonMatchingRows_AndKeepsInstancesWhenCleared()
    {
        var vm = await LoadedAsync(App("a", "Chrome"), App("b", "Firefox"), App("s", "Chrome Helper", RunningAppSection.Background));
        var chrome = vm.Apps.Items.Single(r => r.Key == "a");

        vm.FilterText = "chro";
        Assert.Same(chrome, vm.Apps.Items.Single());
        Assert.Single(vm.Background.Items);
        Assert.Equal("Apps (1)", vm.Apps.HeaderText);

        vm.FilterText = "zzz";
        Assert.True(vm.ShowEmptyState);

        vm.FilterText = string.Empty;
        Assert.Equal(2, vm.Apps.Items.Count);
        Assert.Contains(chrome, vm.Apps.Items);
    }

    [Fact]
    public async Task Polling_RefreshesEveryTwoSeconds_AndStopsWhenCancelled()
    {
        _service.Snapshot = Snapshot(App("a", "A"));
        var vm = Create();
        using var cts = new CancellationTokenSource();
        var task = vm.OnNavigatedToAsync(cts.Token);

        await _service.WaitForSamplesAsync(1);
        _time.Advance(TimeSpan.FromSeconds(2));
        await _service.WaitForSamplesAsync(2);

        await cts.CancelAsync();
        await task;
        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(2, _service.SampleCount);
    }

    [Fact]
    public async Task Polling_SurvivesAFailedRead_AndShowsAPlainError()
    {
        var vm = Create();
        _service.Snapshot = Snapshot(App("a", "A"));
        _service.ThrowOnSample = true;
        using var cts = new CancellationTokenSource();
        var task = vm.OnNavigatedToAsync(cts.Token);
        await _service.WaitForSamplesAsync(1);

        Assert.NotNull(vm.ErrorMessage);

        _service.ThrowOnSample = false;
        _time.Advance(TimeSpan.FromSeconds(2));
        await _service.WaitForSamplesAsync(2);
        await cts.CancelAsync();
        await task;

        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Apps.Items);
    }

    [Fact]
    public async Task EndTask_AsksFirst_ThenEndsAndReportsIt()
    {
        var vm = await LoadedAsync(App("chrome", "Google Chrome", count: 3));

        await vm.EndTaskCommand.ExecuteAsync(vm.Apps.Items[0]);

        Assert.Equal("End Google Chrome? Unsaved work in it will be lost.", _confirmation.LastMessage);
        Assert.Equal(["chrome"], _service.EndedKeys);
        Assert.Equal("Ended Google Chrome.", vm.Message);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task EndTask_WhenConfirmationDeclined_DoesNothing()
    {
        var vm = await LoadedAsync(App("chrome", "Google Chrome"));
        _confirmation.Answer = false;
        var samples = _service.SampleCount;

        await vm.EndTaskCommand.ExecuteAsync(vm.Apps.Items[0]);

        Assert.Empty(_service.EndedKeys);
        Assert.Equal(samples, _service.SampleCount);
        Assert.Null(vm.Message);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task EndTask_OnRowThatCannotBeEnded_DoesNotAskOrEnd()
    {
        var vm = await LoadedAsync(App("shell", "Windows Shell", RunningAppSection.Windows, canEnd: false));

        await vm.EndTaskCommand.ExecuteAsync(vm.Windows.Items[0]);

        Assert.Equal(0, _confirmation.AskCount);
        Assert.Empty(_service.EndedKeys);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Theory]
    [InlineData(EndTaskResult.NeedsAdmin, "needs administrator rights")]
    [InlineData(EndTaskResult.Failed, "Couldn't end Chrome")]
    [InlineData(EndTaskResult.Refused, "Windows needs this")]
    public async Task EndTask_Failures_ShowAPlainErrorLine(EndTaskResult result, string expected)
    {
        var vm = await LoadedAsync(App("chrome", "Chrome"));
        _service.EndResult = result;

        await vm.EndTaskCommand.ExecuteAsync(vm.Apps.Items[0]);

        Assert.Contains(expected, vm.ErrorMessage);
    }

    [Fact]
    public async Task EndTask_NotFound_SaysItIsGone()
    {
        var vm = await LoadedAsync(App("chrome", "Chrome"));
        _service.EndResult = EndTaskResult.NotFound;

        await vm.EndTaskCommand.ExecuteAsync(vm.Apps.Items[0]);

        Assert.Equal("Chrome is no longer running.", vm.Message);
    }

    [Fact]
    public async Task OpenFileLocation_SelectsTheFileInExplorer_OnlyWhenItExists()
    {
        var file = Path.GetTempFileName();
        try
        {
            var vm = await LoadedAsync(
                App("here", "Here", path: file),
                App("gone", "Gone", path: Path.Combine(Path.GetTempPath(), "porchlight-missing-" + Guid.NewGuid() + ".exe")),
                App("none", "None"));

            vm.OpenFileLocationCommand.Execute(vm.Apps.Items.Single(r => r.Key == "gone"));
            vm.OpenFileLocationCommand.Execute(vm.Apps.Items.Single(r => r.Key == "none"));
            Assert.Empty(_runner.StartDetachedCalls);

            vm.OpenFileLocationCommand.Execute(vm.Apps.Items.Single(r => r.Key == "here"));

            var call = Assert.Single(_runner.StartDetachedCalls);
            Assert.Equal("explorer.exe", call.FileName);
            Assert.Equal(["/select,", file], call.Arguments.ToArray());
        }
        finally
        {
            File.Delete(file);
        }
    }

    private sealed class FakeRunningAppsService : IRunningAppsService, IDisposable
    {
        private readonly SemaphoreSlim _sampled = new(0);

        public RunningAppsSnapshot Snapshot { get; set; } = new([], 0, null);

        public void Dispose() => _sampled.Dispose();

        public bool ThrowOnSample { get; set; }

        public int SampleCount { get; private set; }

        public EndTaskResult EndResult { get; set; } = EndTaskResult.Ended;

        public List<string> EndedKeys { get; } = [];

        public Task<RunningAppsSnapshot> SampleAsync(CancellationToken cancellationToken)
        {
            SampleCount++;
            _sampled.Release();
            return ThrowOnSample
                ? Task.FromException<RunningAppsSnapshot>(new InvalidOperationException("boom"))
                : Task.FromResult(Snapshot);
        }

        public Task<EndTaskResult> EndAsync(string groupKey, CancellationToken cancellationToken)
        {
            EndedKeys.Add(groupKey);
            return Task.FromResult(EndResult);
        }

        /// <summary>Waits until <see cref="SampleCount"/> has reached <paramref name="count"/> and the
        /// view model has had the chance to apply that sample.</summary>
        public async Task WaitForSamplesAsync(int count)
        {
            while (SampleCount < count)
            {
                Assert.True(await _sampled.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken), "timed out waiting for a sample");
            }

            // Let the view model finish applying the snapshot that was just handed back.
            await Task.Yield();
        }
    }

    private sealed class FakeConfirmation : IConfirmationDialog
    {
        public bool Answer { get; set; } = true;

        public int AskCount { get; private set; }

        public string LastMessage { get; private set; } = string.Empty;

        public bool Confirm(string title, string message)
        {
            AskCount++;
            LastMessage = message;
            return Answer;
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments)> StartDetachedCalls { get; } = [];

        public Task<ProcessRunResult> RunAsync(
            string fileName, IReadOnlyList<string> arguments, IProgress<string>? onLine, IProgress<string>? onProgress,
            CancellationToken cancellationToken) => Task.FromResult(new ProcessRunResult(0, [], []));

        public void StartDetached(string fileName, IReadOnlyList<string> arguments) =>
            StartDetachedCalls.Add((fileName, arguments));
    }
}
