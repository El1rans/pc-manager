using Porchlight.App.Features.Settings;
using Porchlight.App.Shell;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Settings;
using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.App.Tests.Features.Settings;

public sealed class GeneralSettingsViewModelTests
{
    [Fact]
    public void Defaults_KeepRunningInTrayIsOn_AndStartAtLoginWaitsForTheRealState()
    {
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), new FakeLoginLaunch());

        Assert.True(viewModel.KeepRunningInTray);
        Assert.True(viewModel.IsStartAtLoginBusy);
        Assert.False(viewModel.CanChangeStartAtLogin);
    }

    [Fact]
    public void Loading_DoesNotWriteSettings()
    {
        var store = new FakeSettingsStore();

        _ = new GeneralSettingsViewModel(store, new FakeLoginLaunch());

        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public void ChangingKeepRunningInTray_PersistsItImmediately()
    {
        var store = new FakeSettingsStore();
        var viewModel = new GeneralSettingsViewModel(store, new FakeLoginLaunch());

        viewModel.KeepRunningInTray = false;

        Assert.False(store.Current.Notifications.KeepRunningInTray);
    }

    [Fact]
    public void IsAPageInTheSettingsCategory_FirstTab()
    {
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), new FakeLoginLaunch());

        Assert.Equal(PageCategory.Settings, viewModel.Category);
        Assert.Equal("General", viewModel.TabTitle);
        Assert.Equal(0, viewModel.Order);
    }

    [Fact]
    public async Task NavigatingToThePage_ReadsTheRealTaskState_EachTime()
    {
        var login = new FakeLoginLaunch { Exists = true };
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), login);

        await viewModel.OnNavigatedToAsync(CancellationToken.None);
        Assert.True(viewModel.StartAtLogin);
        Assert.False(viewModel.IsStartAtLoginBusy);

        login.Exists = false;
        await viewModel.OnNavigatedToAsync(CancellationToken.None);

        Assert.False(viewModel.StartAtLogin);
        Assert.Equal(0, login.EnableCalls + login.DisableCalls);
    }

    [Fact]
    public async Task UnreadableTaskState_LeavesTheBoxOffAndEnabled()
    {
        var login = new FakeLoginLaunch { ThrowOnRead = true };
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), login);

        await viewModel.OnNavigatedToAsync(CancellationToken.None);

        Assert.False(viewModel.StartAtLogin);
        Assert.True(viewModel.CanChangeStartAtLogin);
    }

    [Fact]
    public void Theme_DefaultsToMatchWindows()
    {
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), new FakeLoginLaunch());

        Assert.Equal(AppTheme.System, viewModel.SelectedTheme.Value);
        Assert.Equal(["Match Windows", "Light", "Dark"], viewModel.ThemeOptions.Select(o => o.Label));
    }

    [Fact]
    public void SavedTheme_IsSelectedOnOpen_WithoutApplyingOrSaving()
    {
        var store = new FakeSettingsStore();
        store.Current.Appearance.Theme = AppTheme.Dark;
        var themes = new RecordingThemeService();

        var viewModel = new GeneralSettingsViewModel(store, new FakeLoginLaunch(), themes);

        Assert.Equal(AppTheme.Dark, viewModel.SelectedTheme.Value);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Empty(themes.Applied);
    }

    [Fact]
    public void ChoosingATheme_PersistsAndAppliesItImmediately()
    {
        var store = new FakeSettingsStore();
        var themes = new RecordingThemeService();
        var viewModel = new GeneralSettingsViewModel(store, new FakeLoginLaunch(), themes);

        viewModel.SelectedTheme = viewModel.ThemeOptions.Single(o => o.Value == AppTheme.Light);
        viewModel.SelectedTheme = viewModel.ThemeOptions.Single(o => o.Value == AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, store.Current.Appearance.Theme);
        Assert.Equal([AppTheme.Light, AppTheme.Dark], themes.Applied);
    }

    private sealed class RecordingThemeService : IThemeService
    {
        public List<AppTheme> Applied { get; } = [];

        public void Apply(AppTheme theme) => Applied.Add(theme);
    }

    [Fact]
    public async Task LoadStartAtLogin_ReflectsTheRealTaskState_WithoutRunningIt()
    {
        var login = new FakeLoginLaunch { Exists = true };
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), login);
        Assert.True(viewModel.IsStartAtLoginBusy);

        await viewModel.LoadStartAtLoginAsync();

        Assert.True(viewModel.StartAtLogin);
        Assert.False(viewModel.IsStartAtLoginBusy);
        Assert.Equal(0, login.EnableCalls + login.DisableCalls);
    }

    [Fact]
    public async Task TurningStartAtLoginOn_EnablesTheTask()
    {
        var login = new FakeLoginLaunch();
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), login);
        await viewModel.LoadStartAtLoginAsync();

        viewModel.StartAtLogin = true;
        await WaitUntilIdleAsync(viewModel);

        Assert.Equal(1, login.EnableCalls);
        Assert.True(viewModel.StartAtLogin);
        Assert.False(viewModel.HasStartAtLoginError);
    }

    [Fact]
    public async Task TurningStartAtLoginOff_DisablesTheTask()
    {
        var login = new FakeLoginLaunch { Exists = true };
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), login);
        await viewModel.LoadStartAtLoginAsync();

        viewModel.StartAtLogin = false;
        await WaitUntilIdleAsync(viewModel);

        Assert.Equal(1, login.DisableCalls);
        Assert.False(viewModel.StartAtLogin);
    }

    [Fact]
    public async Task DeclinedOrFailedChange_RevertsTheToggleAndShowsTheMessage()
    {
        var login = new FakeLoginLaunch { NextResult = new LoginLaunchResult(LoginLaunchOutcome.Declined, "No permission.") };
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), login);
        await viewModel.LoadStartAtLoginAsync();

        viewModel.StartAtLogin = true;
        await WaitUntilIdleAsync(viewModel);

        Assert.False(viewModel.StartAtLogin);
        Assert.Equal("No permission.", viewModel.StartAtLoginError);
        Assert.True(viewModel.HasStartAtLoginError);
        Assert.Equal(1, login.EnableCalls);
    }

    [Fact]
    public async Task ThrowingService_RevertsTheToggleWithAFriendlyError()
    {
        var login = new FakeLoginLaunch { Throw = true };
        var viewModel = new GeneralSettingsViewModel(new FakeSettingsStore(), login);
        await viewModel.LoadStartAtLoginAsync();

        viewModel.StartAtLogin = true;
        await WaitUntilIdleAsync(viewModel);

        Assert.False(viewModel.StartAtLogin);
        Assert.True(viewModel.HasStartAtLoginError);
        Assert.Equal(1, login.EnableCalls);
    }

    private static async Task WaitUntilIdleAsync(GeneralSettingsViewModel viewModel)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (viewModel.IsStartAtLoginBusy)
        {
            Assert.True(DateTime.UtcNow < deadline, "the change never finished");
            await Task.Delay(10);
        }
    }

    private sealed class FakeLoginLaunch : ILoginLaunchService
    {
        public bool Exists { get; set; }

        public bool Throw { get; set; }

        public bool ThrowOnRead { get; set; }

        public LoginLaunchResult NextResult { get; set; } = LoginLaunchResult.Success;

        public int EnableCalls { get; private set; }

        public int DisableCalls { get; private set; }

        public Task<LoginLaunchState> GetStateAsync(CancellationToken cancellationToken) =>
            ThrowOnRead
                ? throw new InvalidOperationException("boom")
                : Task.FromResult(new LoginLaunchState(Exists, null, null));

        public Task<LoginLaunchResult> EnableAsync(CancellationToken cancellationToken)
        {
            EnableCalls++;
            return Throw ? throw new InvalidOperationException("boom") : Task.FromResult(NextResult);
        }

        public Task<LoginLaunchResult> DisableAsync(CancellationToken cancellationToken)
        {
            DisableCalls++;
            return Throw ? throw new InvalidOperationException("boom") : Task.FromResult(NextResult);
        }

        public Task<bool> RefreshStaleRegistrationAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
