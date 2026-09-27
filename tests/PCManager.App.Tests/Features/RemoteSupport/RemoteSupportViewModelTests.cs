using System.ComponentModel;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using PCManager.App.Controls;
using PCManager.App.Features.RemoteSupport;
using PCManager.App.Tests.Features.Setup;
using PCManager.Core.Components;
using PCManager.Core.RemoteSupport;
using PCManager.Core.Settings;
using Xunit;

namespace PCManager.App.Tests.Features.RemoteSupport;

public sealed class RemoteSupportViewModelTests : IDisposable
{
    private static readonly TimeSpan TestPollInterval = TimeSpan.FromMilliseconds(15);
    private static readonly TimeSpan TestIdWaitTimeout = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeComponentService _componentService = new();
    private readonly FakeAnyDeskService _anyDeskService = new();
    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeUrlLauncher _urlLauncher = new();
    private readonly FakeWindowsVersionReader _windowsVersionReader = new();

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
            _anyDeskService, _componentService, cardFactory, _clipboard, _urlLauncher, _windowsVersionReader,
            _settingsStore, NullLogger<RemoteSupportViewModel>.Instance,
            copyConfirmationDuration: TestPollInterval, waitingForIdPollInterval: TestPollInterval,
            runningStatusPollInterval: TestPollInterval, idWaitTimeout: TestIdWaitTimeout);
    }

    /// <summary>Awaits until <paramref name="condition"/> holds, driven by
    /// <paramref name="notifier"/>'s <see cref="INotifyPropertyChanged.PropertyChanged"/> rather
    /// than a fixed delay, so tests are both fast and not racy against the view model's background
    /// poll loop.</summary>
    private static async Task WaitUntilAsync(
        INotifyPropertyChanged notifier, Func<bool> condition, CancellationToken cancellationToken)
    {
        if (condition())
        {
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (condition())
            {
                tcs.TrySetResult();
            }
        }

        notifier.PropertyChanged += OnChanged;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(WaitTimeout);
        await using var registration = timeoutCts.Token.Register(() => tcs.TrySetCanceled());
        try
        {
            if (condition())
            {
                return;
            }

            await tcs.Task.ConfigureAwait(true);
        }
        finally
        {
            notifier.PropertyChanged -= OnChanged;
        }
    }

    [Fact]
    public void IsPinnedToBottom_True()
    {
        // "Get help" must be placed separately at the bottom of the nav, not among the regular
        // pages ordered by Order - see IPage.IsPinnedToBottom.
        Assert.True(CreateViewModel().IsPinnedToBottom);
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
    public void BeforeFirstLoad_StatusIsHidden()
    {
        // Nit: never show "AnyDesk is not running" + Start before the first state has loaded.
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasLoadedState);
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
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running, "9.7.16", @"C:\AnyDesk.exe"));
        await viewModel.Card.LoadAsync(TestContext.Current.CancellationToken);

        await WaitUntilAsync(viewModel, () => viewModel.HasAddress, TestContext.Current.CancellationToken);

        Assert.False(viewModel.ShowComponentCard);
        Assert.True(viewModel.ShowMainContent);
        Assert.Equal("555 555 555", viewModel.FormattedAddress);
    }

    [Fact]
    public async Task InstalledWithoutId_PollsUntilIdAppearsOnALaterPoll()
    {
        // B2: right after an install, AnyDesk has never run yet, so the first read has no ID -
        // the page must keep polling (starting AnyDesk once) rather than showing
        // "Getting your address..." forever.
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Installed, "9.7.16", @"C:\AnyDesk.exe"));
        var notInstalledYet = new AnyDeskState(
            true, @"C:\AnyDesk.exe", "9.7.16", null, null, false, new ComponentStatus(ComponentState.Installed));
        _anyDeskService.StateToReturn = notInstalledYet;
        _anyDeskService.LaunchResult = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "999888777", null, true, new ComponentStatus(ComponentState.Running));
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        Assert.False(viewModel.HasAddress);

        await WaitUntilAsync(viewModel, () => viewModel.HasAddress, TestContext.Current.CancellationToken);

        Assert.Equal("999 888 777", viewModel.FormattedAddress);
        Assert.Equal(1, _anyDeskService.LaunchCallCount);
    }

    [Fact]
    public async Task InstalledWithoutId_GivesUpAfterTimeoutAndTryAgainRestartsPolling()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Installed, "9.7.16", @"C:\AnyDesk.exe"));
        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", null, null, true, new ComponentStatus(ComponentState.Running));
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        await WaitUntilAsync(viewModel, () => viewModel.ShowAddressTrouble, TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsAddressTimedOut);

        // Try again should clear the timeout and start a fresh wait budget.
        viewModel.TryAgainCommand.Execute(null);
        Assert.False(viewModel.IsAddressTimedOut);
    }

    [Fact]
    public async Task RunningWithId_StillPollsAndNoticesAnyDeskClosing()
    {
        // S6: once an address is known, the page should keep lightly polling so it notices AnyDesk
        // being closed while the page stays open.
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running, "9.7.16", @"C:\AnyDesk.exe"));
        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "123456789", null, true, new ComponentStatus(ComponentState.Running));
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsRunning);

        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "123456789", null, false, new ComponentStatus(ComponentState.Installed));

        await WaitUntilAsync(viewModel, () => !viewModel.IsRunning, TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsRunning);
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
        Assert.False(viewModel.AddressCopyFailed);
    }

    [Fact]
    public async Task CopyAddressCommand_ClipboardFails_ShowsCouldNotCopy()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running));
        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "123456789", null, true, new ComponentStatus(ComponentState.Running));
        _clipboard.NextResult = false;
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        viewModel.CopyAddressCommand.Execute(null);

        Assert.False(viewModel.IsAddressCopied);
        Assert.True(viewModel.AddressCopyFailed);
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
    public async Task CopyAddressThenCopySupportInfo_EachKeepsItsOwnConfirmation()
    {
        // S2: quick Copy address then Copy support info must not leave IsAddressCopied stuck true
        // (or clear it early) - each button's confirmation is on its own timer.
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running));
        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "123456789", null, true, new ComponentStatus(ComponentState.Running));
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        viewModel.CopyAddressCommand.Execute(null);
        viewModel.CopySupportInfoCommand.Execute(null);

        Assert.True(viewModel.IsAddressCopied);
        Assert.True(viewModel.IsSupportInfoCopied);
    }

    [Fact]
    public async Task CopySupportInfoCommand_IncludesComputerNameWindowsVersionAndAddress()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running));
        _anyDeskService.StateToReturn = new(
            true, @"C:\AnyDesk.exe", "9.7.16", "123456789", null, true, new ComponentStatus(ComponentState.Running));
        _windowsVersionReader.Version = "Windows 11 Pro (build 26200)";
        var viewModel = CreateViewModel();
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        viewModel.CopySupportInfoCommand.Execute(null);

        Assert.NotEmpty(_clipboard.Texts);
        Assert.Contains(Environment.MachineName, _clipboard.LastText);
        Assert.Contains("123 456 789", _clipboard.LastText);
        Assert.Contains("Windows 11 Pro (build 26200)", _clipboard.LastText);
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
    public async Task GetStateThrows_ShowsErrorWithRetry()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Running));
        var throwingAnyDeskService = new ThrowingAnyDeskService();
        var cardFactory = new ComponentCardViewModelFactory(_componentService, NullLoggerFactory.Instance);
        var viewModel = new RemoteSupportViewModel(
            throwingAnyDeskService, _componentService, cardFactory, _clipboard, _urlLauncher, _windowsVersionReader,
            _settingsStore, NullLogger<RemoteSupportViewModel>.Instance,
            copyConfirmationDuration: TestPollInterval, waitingForIdPollInterval: TestPollInterval,
            runningStatusPollInterval: TestPollInterval, idWaitTimeout: TestIdWaitTimeout);

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasError);
        Assert.True(viewModel.ShowAddressTrouble);
        Assert.NotEmpty(viewModel.ErrorMessage);

        // Retry clears the error (the fake still throws, so it flips right back to true - what
        // matters here is that the command exists, is bound, and re-runs the read).
        viewModel.TryAgainCommand.Execute(null);
        Assert.True(throwingAnyDeskService.CallCount >= 1);
    }

    [Fact]
    public async Task ShowManualDownloadLink_WhenCardIsInErrorState()
    {
        _componentService.SetStatus(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Error, Message: "Could not start the installer."));
        var viewModel = CreateViewModel();

        await viewModel.Card.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.ShowManualDownloadLink);
    }

    [Fact]
    public void DownloadAnyDeskManuallyCommand_OpensDownloadPage()
    {
        var viewModel = CreateViewModel();

        viewModel.DownloadAnyDeskManuallyCommand.Execute(null);

        Assert.Contains("anydesk.com/download", Assert.Single(_urlLauncher.OpenedUrls));
    }

    [Fact]
    public void SettingHelperName_PersistsToSettingsAndUpdatesStep1Text()
    {
        var viewModel = CreateViewModel();

        viewModel.SetHelperName("Eliran");

        Assert.Equal("Eliran", _settingsStore.Current.RemoteSupport.HelperName);
        Assert.True(viewModel.HasHelperName);
        Assert.Equal("Your helper: Eliran", viewModel.HelperLine);
        Assert.Equal("1. Call Eliran.", viewModel.Step1Text);
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
    public void NoHelperNameSet_HasHelperNameIsFalseAndStep1TextIsGeneric()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasHelperName);
        Assert.Equal("1. Call the person helping you.", viewModel.Step1Text);
    }

    /// <summary><see cref="IAnyDeskService"/> fake that always throws, for the "unexpected error"
    /// path (S5) - a real fault, not just "not installed".</summary>
    private sealed class ThrowingAnyDeskService : IAnyDeskService
    {
        public int CallCount { get; private set; }

        public Task<AnyDeskState> GetStateAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("boom");
        }

        public Task<AnyDeskState> LaunchAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
