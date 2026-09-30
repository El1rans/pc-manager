using Porchlight.App.Features.Notifications;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Settings;
using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.App.Tests.Features.Notifications;

public sealed class NotificationsViewModelTests
{
    [Fact]
    public void Defaults_AreOn_WithDailyChecks()
    {
        var viewModel = new NotificationsViewModel(new FakeSettingsStore(), new FakeLoginLaunch());

        Assert.True(viewModel.KeepRunningInTray);
        Assert.True(viewModel.AlertLowDisk);
        Assert.True(viewModel.AlertTemperature);
        Assert.True(viewModel.AlertUpdates);
        Assert.True(viewModel.AlertRestartPending);
        Assert.Equal(UpdateCheckSchedule.Daily, viewModel.SelectedSchedule.Value);
    }

    [Fact]
    public void Loading_DoesNotWriteSettings()
    {
        var store = new FakeSettingsStore();

        _ = new NotificationsViewModel(store, new FakeLoginLaunch());

        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public void ChangingToggles_PersistsThemImmediately()
    {
        var store = new FakeSettingsStore();
        var viewModel = new NotificationsViewModel(store, new FakeLoginLaunch());

        viewModel.KeepRunningInTray = false;
        viewModel.AlertLowDisk = false;
        viewModel.AlertTemperature = false;
        viewModel.AlertUpdates = false;
        viewModel.AlertRestartPending = false;

        var saved = store.Current.Notifications;
        Assert.False(saved.KeepRunningInTray);
        Assert.False(saved.AlertLowDisk);
        Assert.False(saved.AlertTemperature);
        Assert.False(saved.AlertUpdates);
        Assert.False(saved.AlertRestartPending);
    }

    [Fact]
    public void ChoosingASchedule_PersistsIt()
    {
        var store = new FakeSettingsStore();
        var viewModel = new NotificationsViewModel(store, new FakeLoginLaunch());

        viewModel.SelectedSchedule = viewModel.ScheduleOptions.Single(o => o.Value == UpdateCheckSchedule.Never);

        Assert.Equal(UpdateCheckSchedule.Never, store.Current.Notifications.UpdateCheckSchedule);
    }

    [Fact]
    public void SavedSchedule_IsSelectedOnOpen()
    {
        var store = new FakeSettingsStore();
        store.Current.Notifications.UpdateCheckSchedule = UpdateCheckSchedule.Weekly;

        var viewModel = new NotificationsViewModel(store, new FakeLoginLaunch());

        Assert.Equal(UpdateCheckSchedule.Weekly, viewModel.SelectedSchedule.Value);
    }

    [Fact]
    public async Task LoadStartAtLogin_ReflectsTheRealTaskState_WithoutRunningIt()
    {
        var login = new FakeLoginLaunch { Exists = true };
        var viewModel = new NotificationsViewModel(new FakeSettingsStore(), login);
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
        var viewModel = new NotificationsViewModel(new FakeSettingsStore(), login);
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
        var viewModel = new NotificationsViewModel(new FakeSettingsStore(), login);
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
        var viewModel = new NotificationsViewModel(new FakeSettingsStore(), login);
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
        var viewModel = new NotificationsViewModel(new FakeSettingsStore(), login);
        await viewModel.LoadStartAtLoginAsync();

        viewModel.StartAtLogin = true;
        await WaitUntilIdleAsync(viewModel);

        Assert.False(viewModel.StartAtLogin);
        Assert.True(viewModel.HasStartAtLoginError);
        Assert.Equal(1, login.EnableCalls);
    }

    private static async Task WaitUntilIdleAsync(NotificationsViewModel viewModel)
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

        public LoginLaunchResult NextResult { get; set; } = LoginLaunchResult.Success;

        public int EnableCalls { get; private set; }

        public int DisableCalls { get; private set; }

        public Task<LoginLaunchState> GetStateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new LoginLaunchState(Exists, null, null));

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
