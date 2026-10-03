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

    [Fact]
    public void CheckupReminder_DefaultsToOffWeeklySunday()
    {
        var viewModel = new NotificationSettingsViewModel(new FakeSettingsStore());

        Assert.False(viewModel.CheckupReminderEnabled);
        Assert.Equal(CheckupReminderFrequency.Weekly, viewModel.SelectedCheckupFrequency.Value);
        Assert.Equal(DayOfWeek.Sunday, viewModel.SelectedCheckupDay.Value);
    }

    [Fact]
    public void TurningReminderOnAndOff_PersistsAndStampsTheDay()
    {
        var store = new FakeSettingsStore();
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero));
        var viewModel = new NotificationSettingsViewModel(store, time);

        viewModel.CheckupReminderEnabled = true;
        Assert.True(store.Current.CheckupReminder.Enabled);
        Assert.Equal(time.GetUtcNow(), store.Current.CheckupReminder.EnabledSinceUtc);

        viewModel.CheckupReminderEnabled = false;
        Assert.False(store.Current.CheckupReminder.Enabled);
        Assert.Null(store.Current.CheckupReminder.EnabledSinceUtc);
    }

    [Fact]
    public void ChoosingFrequencyAndDay_PersistsThem_AndLoadingDoesNot()
    {
        var store = new FakeSettingsStore();
        var viewModel = new NotificationSettingsViewModel(store);
        Assert.Equal(0, store.UpdateCallCount);

        viewModel.SelectedCheckupFrequency = viewModel.CheckupFrequencyOptions.Single(o => o.Value == CheckupReminderFrequency.Monthly);
        viewModel.SelectedCheckupDay = viewModel.CheckupDayOptions.Single(o => o.Value == DayOfWeek.Friday);

        Assert.Equal(CheckupReminderFrequency.Monthly, store.Current.CheckupReminder.Frequency);
        Assert.Equal(DayOfWeek.Friday, store.Current.CheckupReminder.Day);
    }
}
