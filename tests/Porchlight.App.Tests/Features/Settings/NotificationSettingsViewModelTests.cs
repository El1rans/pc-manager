using Porchlight.App.Features.Settings;
using Porchlight.App.Shell;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Features.Settings;

public sealed class NotificationSettingsViewModelTests
{
    [Fact]
    public void Defaults_AreOn_WithDailyChecks()
    {
        var viewModel = new NotificationSettingsViewModel(new FakeSettingsStore());

        Assert.True(viewModel.AlertLowDisk);
        Assert.True(viewModel.AlertTemperature);
        Assert.True(viewModel.AlertUpdates);
        Assert.True(viewModel.AlertRestartPending);
        Assert.Equal(UpdateCheckSchedule.Daily, viewModel.SelectedSchedule.Value);
        Assert.Equal(["Every day", "Every week", "Never"], viewModel.ScheduleOptions.Select(o => o.Label));
    }

    [Fact]
    public void Loading_DoesNotWriteSettings()
    {
        var store = new FakeSettingsStore();

        _ = new NotificationSettingsViewModel(store);

        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public void ChangingAlerts_PersistsThemImmediately()
    {
        var store = new FakeSettingsStore();
        var viewModel = new NotificationSettingsViewModel(store);

        viewModel.AlertLowDisk = false;
        viewModel.AlertTemperature = false;
        viewModel.AlertUpdates = false;
        viewModel.AlertRestartPending = false;

        var saved = store.Current.Notifications;
        Assert.False(saved.AlertLowDisk);
        Assert.False(saved.AlertTemperature);
        Assert.False(saved.AlertUpdates);
        Assert.False(saved.AlertRestartPending);
    }

    [Fact]
    public void ChangingAlerts_LeavesKeepRunningInTrayAlone()
    {
        var store = new FakeSettingsStore();
        var viewModel = new NotificationSettingsViewModel(store);

        viewModel.AlertLowDisk = false;

        Assert.True(store.Current.Notifications.KeepRunningInTray);
    }

    [Fact]
    public void ChoosingASchedule_PersistsIt()
    {
        var store = new FakeSettingsStore();
        var viewModel = new NotificationSettingsViewModel(store);

        viewModel.SelectedSchedule = viewModel.ScheduleOptions.Single(o => o.Value == UpdateCheckSchedule.Never);

        Assert.Equal(UpdateCheckSchedule.Never, store.Current.Notifications.UpdateCheckSchedule);
    }

    [Fact]
    public void SavedSchedule_IsSelectedOnOpen()
    {
        var store = new FakeSettingsStore();
        store.Current.Notifications.UpdateCheckSchedule = UpdateCheckSchedule.Weekly;

        var viewModel = new NotificationSettingsViewModel(store);

        Assert.Equal(UpdateCheckSchedule.Weekly, viewModel.SelectedSchedule.Value);
    }

    [Fact]
    public void IsAPageInTheSettingsCategory_SecondTab()
    {
        var viewModel = new NotificationSettingsViewModel(new FakeSettingsStore());

        Assert.Equal(PageCategory.Settings, viewModel.Category);
        Assert.Equal("Notifications", viewModel.TabTitle);
        Assert.Equal(1, viewModel.Order);
    }
}
