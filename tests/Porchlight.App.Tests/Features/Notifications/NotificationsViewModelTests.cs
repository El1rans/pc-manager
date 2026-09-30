using Porchlight.App.Features.Notifications;
using Porchlight.App.Shell;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Features.Notifications;

public sealed class NotificationsViewModelTests
{
    [Fact]
    public void Defaults_AreOn_WithDailyChecks()
    {
        var viewModel = new NotificationsViewModel(new FakeSettingsStore());

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

        _ = new NotificationsViewModel(store);

        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public void ChangingToggles_PersistsThemImmediately()
    {
        var store = new FakeSettingsStore();
        var viewModel = new NotificationsViewModel(store);

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
        var viewModel = new NotificationsViewModel(store);

        viewModel.SelectedSchedule = viewModel.ScheduleOptions.Single(o => o.Value == UpdateCheckSchedule.Never);

        Assert.Equal(UpdateCheckSchedule.Never, store.Current.Notifications.UpdateCheckSchedule);
    }

    [Fact]
    public void SavedSchedule_IsSelectedOnOpen()
    {
        var store = new FakeSettingsStore();
        store.Current.Notifications.UpdateCheckSchedule = UpdateCheckSchedule.Weekly;

        var viewModel = new NotificationsViewModel(store);

        Assert.Equal(UpdateCheckSchedule.Weekly, viewModel.SelectedSchedule.Value);
    }

    [Fact]
    public void Theme_DefaultsToMatchWindows()
    {
        var viewModel = new NotificationsViewModel(new FakeSettingsStore());

        Assert.Equal(AppTheme.System, viewModel.SelectedTheme.Value);
        Assert.Equal(["Match Windows", "Light", "Dark"], viewModel.ThemeOptions.Select(o => o.Label));
    }

    [Fact]
    public void SavedTheme_IsSelectedOnOpen_WithoutApplyingOrSaving()
    {
        var store = new FakeSettingsStore();
        store.Current.Appearance.Theme = AppTheme.Dark;
        var themes = new RecordingThemeService();

        var viewModel = new NotificationsViewModel(store, themes);

        Assert.Equal(AppTheme.Dark, viewModel.SelectedTheme.Value);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Empty(themes.Applied);
    }

    [Fact]
    public void ChoosingATheme_PersistsAndAppliesItImmediately()
    {
        var store = new FakeSettingsStore();
        var themes = new RecordingThemeService();
        var viewModel = new NotificationsViewModel(store, themes);

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
}
