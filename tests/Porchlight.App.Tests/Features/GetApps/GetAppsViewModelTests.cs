using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.GetApps;
using Porchlight.App.Features.Updates;
using Porchlight.App.Shell;
using Porchlight.App.Tests.Features.Updates;
using Porchlight.Core.Processes;
using Porchlight.Core.Settings;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.App.Tests.Features.GetApps;

public sealed class GetAppsViewModelTests : IDisposable
{
    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeWingetClient _winget = new();

    public GetAppsViewModelTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PorchlightAppTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _settingsStore = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_directory, "settings.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private GetAppsViewModel Create() => new(_winget, _settingsStore, NullLogger<GetAppsViewModel>.Instance);

    private static WingetSearchResult Result(string id, string name = "", string version = "1.0") =>
        new(string.IsNullOrEmpty(name) ? id : name, id, version);

    [Fact]
    public void PageMetadata_IsFirstTabOfAppsCategory()
    {
        using var vm = Create();

        Assert.Equal("Get apps", vm.Title);
        Assert.Equal(PageCategory.Apps, vm.Category);
        Assert.Equal(1, vm.Order);
        Assert.IsAssignableFrom<IBusyGuard>(vm);
    }

    [Fact]
    public void EmptyBox_ShowsPopularApps()
    {
        using var vm = Create();

        Assert.True(vm.ShowPopular);
        Assert.Equal(PopularApps.All.Select(a => a.Id), vm.PopularApps.Select(a => a.Id));
    }

    [Fact]
    public async Task Search_TooShortQuery_DoesNotCallWinget()
    {
        using var vm = Create();
        vm.Query = "v";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(_winget.SearchCalls);
        Assert.Equal(GetAppsViewModel.QueryTooShortText, vm.StatusText);
        Assert.True(vm.ShowPopular);
    }

    [Fact]
    public async Task Search_MapsResultsAndCountsThem()
    {
        _winget.SearchResults.AddRange([Result("VideoLAN.VLC", "VLC media player", "3.0.23"), Result("Other.App")]);
        using var vm = Create();
        vm.Query = "  vlc ";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal(["vlc"], _winget.SearchCalls);
        Assert.Equal(2, vm.Results.Count);
        Assert.Equal("VLC media player", vm.Results[0].Name);
        Assert.Equal("3.0.23", vm.Results[0].Version);
        Assert.Equal("2 apps found", vm.StatusText);
        Assert.False(vm.ShowPopular);
        Assert.False(vm.IsSearching);
    }

    [Fact]
    public async Task Search_CapsResultsAtFifty()
    {
        _winget.SearchResults.AddRange(Enumerable.Range(0, 80).Select(i => Result("Vendor.App" + i)));
        using var vm = Create();
        vm.Query = "app";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal(GetAppsViewModel.MaximumResults, vm.Results.Count);
    }

    [Fact]
    public async Task Search_NoResults_ShowsHelpfulMessage()
    {
        using var vm = Create();
        vm.Query = "xyz";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
        Assert.Equal("No apps found for \"xyz\". Check the spelling or try a shorter name.", vm.StatusText);
        Assert.False(vm.ShowPopular);
    }

    [Fact]
    public async Task Search_ShowsSearchingWhileRunning()
    {
        _winget.SearchGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var vm = Create();
        vm.Query = "vlc";

        var task = vm.SearchCommand.ExecuteAsync(null);

        Assert.True(vm.IsSearching);
        Assert.Equal(GetAppsViewModel.SearchingText, vm.StatusText);

        _winget.SearchGate.SetResult();
        await task;
        Assert.False(vm.IsSearching);
    }

    [Fact]
    public async Task Search_WingetMissing_ShowsItsMessage()
    {
        _winget.SearchException = new WingetNotFoundException("winget.exe was not found.", innerException: null!);
        using var vm = Create();
        vm.Query = "vlc";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal("winget.exe was not found.", vm.ErrorText);
        Assert.False(vm.IsSearching);
    }

    [Fact]
    public async Task Search_OtherFailure_ShowsFriendlyError()
    {
        _winget.SearchException = new InvalidOperationException("boom 0x8A15006D");
        using var vm = Create();
        vm.Query = "vlc";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal(GetAppsViewModel.SearchFailedText, vm.ErrorText);
        Assert.DoesNotContain("0x", vm.ErrorText);
    }

    [Fact]
    public async Task Search_NewSearchSupersedesOldOne()
    {
        _winget.SearchGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _winget.SearchResults.Add(Result("Late.App"));
        using var vm = Create();
        vm.Query = "first";
        var first = vm.SearchCommand.ExecuteAsync(null);

        vm.Query = "second";
        _winget.SearchGate.SetResult();
        await vm.SearchCommand.ExecuteAsync(null);
        await first;

        Assert.False(vm.IsSearching);
        Assert.Equal("1 app found", vm.StatusText);
    }

    [Fact]
    public async Task ClearingTheBox_ReturnsToPopularApps()
    {
        _winget.SearchResults.Add(Result("A.App"));
        using var vm = Create();
        vm.Query = "app";
        await vm.SearchCommand.ExecuteAsync(null);

        vm.Query = string.Empty;

        Assert.True(vm.ShowPopular);
        Assert.Empty(vm.Results);
        Assert.Null(vm.StatusText);
    }

    [Fact]
    public async Task OnNavigatedTo_MarksInstalledAppsInSearchAndPopular()
    {
        _winget.InstalledIds.Add("videolan.vlc");
        _winget.SearchResults.AddRange([Result("VideoLAN.VLC"), Result("Other.App")]);
        using var vm = Create();
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        await vm.InstalledIdsTask;
        vm.Query = "vlc";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(vm.Results[0].IsInstalled);
        Assert.False(vm.Results[1].IsInstalled);
        Assert.True(vm.PopularApps.Single(a => a.Id == "VideoLAN.VLC").IsInstalled);
        Assert.False(vm.Results[0].InstallCommand.CanExecute(null));
    }

    [Fact]
    public async Task InstalledState_ArrivesLaterThanResults_UpdatesExistingRows()
    {
        _winget.ListGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _winget.InstalledIds.Add("Google.Chrome");
        using var vm = Create();
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        Assert.False(vm.PopularApps.Single(a => a.Id == "Google.Chrome").IsInstalled);

        _winget.ListGate.SetResult();
        await vm.InstalledIdsTask;

        Assert.True(vm.PopularApps.Single(a => a.Id == "Google.Chrome").IsInstalled);
    }

    [Fact]
    public async Task Install_Success_MarksInstalledAndUsesSilentSetting()
    {
        _settingsStore.Update(s => s.Updates.Silent = true);
        using var vm = Create();
        var row = vm.PopularApps.Single(a => a.Id == "VideoLAN.VLC");

        await row.InstallCommand.ExecuteAsync(null);

        Assert.Equal(["VideoLAN.VLC"], _winget.InstallCalls);
        Assert.Equal([true], _winget.InstallSilentFlags);
        Assert.True(row.IsInstalled);
        Assert.False(row.IsInstalling);
        Assert.Equal("Installed", row.ResultText);
        Assert.False(row.ResultIsError);
        Assert.False(row.ShowResultText);
        Assert.False(vm.IsBusyWithWork);
    }

    [Fact]
    public async Task Install_NotSilentByDefault()
    {
        using var vm = Create();

        await vm.PopularApps[0].InstallCommand.ExecuteAsync(null);

        Assert.Equal([false], _winget.InstallSilentFlags);
    }

    [Fact]
    public async Task Install_Success_AlsoMarksSameIdInSearchResults()
    {
        _winget.SearchResults.Add(Result("VideoLAN.VLC"));
        using var vm = Create();
        vm.Query = "vlc";
        await vm.SearchCommand.ExecuteAsync(null);

        await vm.PopularApps.Single(a => a.Id == "VideoLAN.VLC").InstallCommand.ExecuteAsync(null);

        Assert.True(vm.Results[0].IsInstalled);
    }

    [Fact]
    public async Task Install_RestartNeeded_IsSuccessWithNote()
    {
        _winget.InstallResult = new WingetResult(WingetExitCodes.InstallRebootRequiredToFinish, []);
        using var vm = Create();
        var row = vm.PopularApps[0];

        await row.InstallCommand.ExecuteAsync(null);

        Assert.True(row.IsInstalled);
        Assert.Equal("Installed. Restart your PC to finish.", row.ResultText);
        Assert.True(row.ShowResultText);
    }

    [Fact]
    public async Task Install_AlreadyInstalledExitCode_CountsAsInstalled()
    {
        _winget.InstallResult = new WingetResult(WingetExitCodes.PackageAlreadyInstalled, []);
        using var vm = Create();
        var row = vm.PopularApps[0];

        await row.InstallCommand.ExecuteAsync(null);

        Assert.True(row.IsInstalled);
    }

    [Fact]
    public async Task Install_Failure_ShowsFriendlyTextWithoutBareCode()
    {
        _winget.InstallResult = new WingetResult(WingetExitCodes.InstallNoNetwork, []);
        using var vm = Create();
        var row = vm.PopularApps[0];

        await row.InstallCommand.ExecuteAsync(null);

        Assert.False(row.IsInstalled);
        Assert.True(row.ResultIsError);
        Assert.False(string.IsNullOrWhiteSpace(row.ResultText));
        Assert.DoesNotContain("0x", row.ResultText);
        Assert.True(row.InstallCommand.CanExecute(null));
        Assert.False(vm.IsBusyWithWork);
    }

    [Fact]
    public async Task Install_UnknownExitCode_ShowsGenericFriendlyFailure()
    {
        _winget.InstallResult = new WingetResult(unchecked((int)0x8A15FFFF), []);
        using var vm = Create();
        var row = vm.PopularApps[0];

        await row.InstallCommand.ExecuteAsync(null);

        Assert.True(row.ResultIsError);
        Assert.DoesNotContain("0x", row.ResultText);
    }

    [Fact]
    public async Task Install_UnexpectedException_ShowsFriendlyFailureAndReleasesBusy()
    {
        _winget.InstallException = new InvalidOperationException("boom");
        using var vm = Create();
        var row = vm.PopularApps[0];

        await row.InstallCommand.ExecuteAsync(null);

        Assert.Equal(GetAppsViewModel.InstallFailedText, row.ResultText);
        Assert.True(row.ResultIsError);
        Assert.False(vm.IsBusyWithWork);
    }

    [Fact]
    public async Task Install_WingetMissing_ShowsItsMessageOnTheRow()
    {
        _winget.InstallException = new WingetNotFoundException("winget.exe was not found.", innerException: null!);
        using var vm = Create();
        var row = vm.PopularApps[0];

        await row.InstallCommand.ExecuteAsync(null);

        Assert.Equal("winget.exe was not found.", row.ResultText);
        Assert.True(row.ResultIsError);
    }

    [Fact]
    public async Task Install_OnlyOneAtATime_AndBusyGuardWhileRunning()
    {
        _winget.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var vm = Create();
        var first = vm.PopularApps[0];
        var second = vm.PopularApps[1];

        var running = first.InstallCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusyWithWork);
        Assert.True(first.IsInstalling);
        Assert.False(first.InstallCommand.CanExecute(null));
        Assert.False(second.InstallCommand.CanExecute(null));

        // Even if something invokes it anyway, a second install never starts.
        await vm.InstallAsync(second);
        Assert.Equal([first.Id], _winget.InstallCalls);

        _winget.Gate.SetResult();
        await running;

        Assert.False(vm.IsBusyWithWork);
        Assert.True(second.InstallCommand.CanExecute(null));
    }

    [Fact]
    public async Task Install_ShowsWingetProgressTextWhileRunning()
    {
        _winget.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _winget.InstallProgressLines.Add("Downloading 1 MB / 5 MB");
        using var vm = Create();
        var row = vm.PopularApps[0];

        var running = row.InstallCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => row.ProgressText is not null);

        Assert.Equal("Downloading 1 MB / 5 MB", row.ProgressText);

        _winget.Gate.SetResult();
        await running;
        Assert.Null(row.ProgressText);
    }

    [Fact]
    public async Task Install_RefusedForIdNotOfferedByThePage()
    {
        using var vm = Create();
        var stranger = new AppResultViewModel("Evil", "Evil.App", "1.0", vm.InstallAsync, () => false);

        await vm.InstallAsync(stranger);

        Assert.Empty(_winget.InstallCalls);
        Assert.False(stranger.IsInstalling);
    }

    [Fact]
    public async Task Install_RefusedForRowFromAnOlderSearch()
    {
        _winget.SearchResults.Add(Result("Old.App"));
        using var vm = Create();
        vm.Query = "old";
        await vm.SearchCommand.ExecuteAsync(null);
        var oldRow = vm.Results[0];

        _winget.SearchResults.Clear();
        _winget.SearchResults.Add(Result("New.App"));
        vm.Query = "new";
        await vm.SearchCommand.ExecuteAsync(null);

        await vm.InstallAsync(oldRow);

        Assert.Empty(_winget.InstallCalls);
    }

    [Fact]
    public async Task Install_RefusedForOptionLikeId()
    {
        _winget.SearchResults.Add(Result("--source"));
        using var vm = Create();
        vm.Query = "x1";
        await vm.SearchCommand.ExecuteAsync(null);

        await vm.InstallAsync(vm.Results[0]);

        Assert.Empty(_winget.InstallCalls);
    }

    [Fact]
    public async Task Install_StillRunsAfterNavigatingAwayAndBack()
    {
        _winget.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var vm = Create();
        var running = vm.PopularApps[0].InstallCommand.ExecuteAsync(null);

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.IsBusyWithWork);
        _winget.Gate.SetResult();
        await running;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
