using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using PCManager.App.Controls;
using PCManager.App.Features.RemoteSupport;
using PCManager.App.Tests.Features.Setup;
using PCManager.Core.Components;
using PCManager.Core.Settings;
using Xunit;

namespace PCManager.App.Tests.Features.RemoteSupport;

public sealed class RemoteSupportViewModelTests : IDisposable
{
    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeComponentService _componentService = new();
    private readonly FakeAnyDeskService _anyDeskService = new();
    private readonly FakeClipboardService _clipboard = new();

    public RemoteSupportViewModelTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PCManagerAppTests_" + Guid.NewGuid().ToString("N"));
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

    private RemoteSupportViewModel CreateViewModel()
    {
        var cardFactory = new ComponentCardViewModelFactory(_componentService, NullLoggerFactory.Instance);
        return new RemoteSupportViewModel(
            _anyDeskService, _componentService, cardFactory, _clipboard, _settingsStore,
            NullLogger<RemoteSupportViewModel>.Instance);
    }

    [Fact]
    public void Order_IsLast()
    {
        // Nav order 0-3 are taken by Dashboard/Updates/Hardware/Lighting; "Get help" must be last.
        Assert.Equal(4, CreateViewModel().Order);
    }

    [Fact]
    public async Task NotInstalled_ShowsComponentCardNotMainContent()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, ComponentStatus.NotInstalled);
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.ShowComponentCard);
        Assert.False(viewModel.ShowMainContent);
    }

    [Fact]
    public async Task Installed_ShowsMainContentWithAddressFromAnyDeskService()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running, "9.7.16", @"C:\AnyDesk.exe"));
        _anyDeskService.StateToReturn = new(
            IsInstalled: true, ExePath: @"C:\AnyDesk.exe", Version: "9.7.16",
            Id: "123456789", Alias: null, IsRunning: true, ComponentStatus: new ComponentStatus(ComponentState.Running));
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.ShowComponentCard);
        Assert.True(viewModel.ShowMainContent);
        Assert.Equal("123 456 789", viewModel.FormattedAddress);
        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.ShowStartButton);
    }

    [Fact]
    public async Task Installed_NotRunning_ShowsStartButton()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Installed, "9.7.16", @"C:\AnyDesk.exe"));
        _anyDeskService.StateToReturn = new(
            IsInstalled: true, ExePath: @"C:\AnyDesk.exe", Version: "9.7.16",
            Id: "123456789", Alias: null, IsRunning: false, ComponentStatus: new ComponentStatus(ComponentState.Installed));
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.ShowMainContent);
        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.ShowStartButton);
        Assert.Equal("AnyDesk is not running", viewModel.StatusText);
    }

    [Fact]
    public async Task InstalledMidSession_TransitionsFromCardToMainContentWithoutRenavigating()
    {
        // Starts not installed - the card is showing, like on first navigation to the page.
        _componentService.SetStatus(ComponentIds.AnyDesk, ComponentStatus.NotInstalled);
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.ShowComponentCard);

        // AnyDesk gets installed from elsewhere (its own card's Install button, or first-run setup)
        // while this page is still open - IComponentService.StatusChanged is how every page hears
        // about that without polling or restarting.
        _anyDeskService.StateToReturn = new(
            IsInstalled: true, ExePath: @"C:\AnyDesk.exe", Version: "9.7.16",
            Id: "555555555", Alias: null, IsRunning: true, ComponentStatus: new ComponentStatus(ComponentState.Running));
        await viewModel.Card.LoadAsync(TestContext.Current.CancellationToken);
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running, "9.7.16", @"C:\AnyDesk.exe"));
        _componentService.InstallCalls.Clear();
        await viewModel.Card.LoadAsync(TestContext.Current.CancellationToken);

        // Let the fire-and-forget refresh triggered by the card's Status change complete.
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.False(viewModel.ShowComponentCard);
        Assert.True(viewModel.ShowMainContent);
        Assert.Equal("555 555 555", viewModel.FormattedAddress);
    }

    [Fact]
    public async Task CopyAddressCommand_PutsDigitsOnlyOnClipboard()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running));
        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "123456789", null, true, new ComponentStatus(ComponentState.Running));
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        viewModel.CopyAddressCommand.Execute(null);

        Assert.Equal("123456789", _clipboard.LastText);
        Assert.True(viewModel.IsAddressCopied);
    }

    [Fact]
    public async Task CopyAddressCommand_NoAddressYet_DoesNothing()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Installed));
        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", null, null, false, new ComponentStatus(ComponentState.Installed));
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        viewModel.CopyAddressCommand.Execute(null);

        Assert.Empty(_clipboard.Texts);
    }

    [Fact]
    public async Task CopySupportInfoCommand_IncludesComputerNameAndAddress()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running));
        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "123456789", null, true, new ComponentStatus(ComponentState.Running));
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        viewModel.CopySupportInfoCommand.Execute(null);

        Assert.NotEmpty(_clipboard.Texts);
        Assert.Contains(Environment.MachineName, _clipboard.LastText);
        Assert.Contains("123 456 789", _clipboard.LastText);
        Assert.True(viewModel.IsSupportInfoCopied);
    }

    [Fact]
    public async Task StartAnyDeskCommand_LaunchesAndUpdatesState()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Installed));
        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "123456789", null, false, new ComponentStatus(ComponentState.Installed));
        _anyDeskService.LaunchResult = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "123456789", null, true, new ComponentStatus(ComponentState.Running));
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.ShowStartButton);

        await viewModel.StartAnyDeskCommand.ExecuteAsync(null);

        Assert.Equal(1, _anyDeskService.LaunchCallCount);
        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.ShowStartButton);
    }

    [Fact]
    public void SettingHelperName_PersistsToSettings()
    {
        var viewModel = CreateViewModel();

        viewModel.HelperName = "Eliran";

        Assert.Equal("Eliran", _settingsStore.Current.RemoteSupport.HelperName);
        Assert.True(viewModel.HasHelperName);
    }

    [Fact]
    public void Constructor_LoadsExistingHelperNameWithoutRewritingSettings()
    {
        _settingsStore.Update(s => s.RemoteSupport.HelperName = "Mom");

        var viewModel = CreateViewModel();

        Assert.Equal("Mom", viewModel.HelperName);
        Assert.True(viewModel.HasHelperName);
    }

    [Fact]
    public void NoHelperNameSet_HasHelperNameIsFalse()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasHelperName);
    }
}
