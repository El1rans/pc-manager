using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Controls;
using Porchlight.App.Tests.Features.RemoteSupport;
using Porchlight.App.Tests.Features.Setup;
using Porchlight.Core.Components;
using Porchlight.Core.Lighting;
using Xunit;

namespace Porchlight.App.Tests.Features.Lighting;

public sealed class LightingViewModelTests
{
    private static (
        Porchlight.App.Features.Lighting.LightingViewModel ViewModel,
        FakeSettingsStore Settings,
        FakeLightingService LightingService,
        FakeLightingConflictDetector ConflictDetector,
        FakeUrlLauncher UrlLauncher,
        FakeComponentService ComponentService) CreateViewModelWithDependencies(
        IEnumerable<string>? seedFavorites = null)
    {
        var settings = new FakeSettingsStore();
        if (seedFavorites is not null)
        {
            settings.Current.Lighting.FavoriteColors = seedFavorites.ToList();
        }

        var componentService = new FakeComponentService();
        var factory = new ComponentCardViewModelFactory(componentService, NullLoggerFactory.Instance);
        var lightingService = new FakeLightingService();
        var conflictDetector = new FakeLightingConflictDetector();
        var urlLauncher = new FakeUrlLauncher();

        var viewModel = new Porchlight.App.Features.Lighting.LightingViewModel(
            factory, lightingService, conflictDetector, urlLauncher, settings, NullLoggerFactory.Instance);

        return (viewModel, settings, lightingService, conflictDetector, urlLauncher, componentService);
    }

    private static (Porchlight.App.Features.Lighting.LightingViewModel ViewModel, FakeSettingsStore Settings) CreateViewModel(
        IEnumerable<string>? seedFavorites = null)
    {
        var (viewModel, settings, _, _, _, _) = CreateViewModelWithDependencies(seedFavorites);
        return (viewModel, settings);
    }

    [Fact]
    public void Construction_NormalizesInvalidDuplicateAndOverCapFavoritesFromSettings()
    {
        var seed = new[] { "#FF0000", "ff0000", "not-a-color", "#00FF00", "#0000FF", "#111111", "#222222", "#333333", "#444444", "#555555" };

        var (viewModel, _) = CreateViewModel(seed);

        // "#FF0000" and "ff0000" are the same color (case-insensitive) and must not be duplicated;
        // the invalid entry is dropped; the list is capped at 8.
        Assert.Equal(8, viewModel.FavoriteColors.Count);
        Assert.Equal("#FF0000", viewModel.FavoriteColors[0]);
        Assert.DoesNotContain("NOT-A-COLOR", viewModel.FavoriteColors);
    }

    [Fact]
    public void SaveFavorite_ValidColor_AddsNormalizedHexAndPersists()
    {
        var (viewModel, settings) = CreateViewModel();
        viewModel.SelectedColorHex = "#abcdef";

        viewModel.SaveFavoriteCommand.Execute(null);

        Assert.Equal(["#ABCDEF"], viewModel.FavoriteColors);
        Assert.Equal(["#ABCDEF"], settings.Current.Lighting.FavoriteColors);
        Assert.Equal(1, settings.UpdateCallCount);
    }

    [Fact]
    public void SaveFavorite_AlreadySaved_DoesNotAddDuplicateOrPersistAgain()
    {
        var (viewModel, settings) = CreateViewModel();
        viewModel.SelectedColorHex = "#FF0000";
        viewModel.SaveFavoriteCommand.Execute(null);
        var callsAfterFirstSave = settings.UpdateCallCount;

        viewModel.SaveFavoriteCommand.Execute(null);

        Assert.Single(viewModel.FavoriteColors);
        Assert.Equal(callsAfterFirstSave, settings.UpdateCallCount);
    }

    [Fact]
    public void SaveFavorite_AtCapacity_CommandCannotExecute()
    {
        var eightDistinctColors = Enumerable.Range(0, 8).Select(i => $"#{i}{i}{i}{i}{i}{i}");
        var (viewModel, settings) = CreateViewModel(eightDistinctColors);

        Assert.False(viewModel.CanSaveFavorite);
        // A real button bound to this command would be disabled and never invoke it - mirror that
        // instead of calling Execute() directly, which (like any ICommand) does not self-guard.
        Assert.False(viewModel.SaveFavoriteCommand.CanExecute(null));

        Assert.Equal(8, viewModel.FavoriteColors.Count);
        Assert.Equal(0, settings.UpdateCallCount);
    }

    [Fact]
    public void RemoveFavorite_Present_RemovesAndPersists()
    {
        var (viewModel, settings) = CreateViewModel(["#FF0000", "#00FF00"]);

        viewModel.RemoveFavoriteCommand.Execute("#FF0000");

        Assert.Equal(["#00FF00"], viewModel.FavoriteColors);
        Assert.Equal(["#00FF00"], settings.Current.Lighting.FavoriteColors);
        Assert.Equal(1, settings.UpdateCallCount);
    }

    [Fact]
    public void RemoveFavorite_NotPresent_DoesNothing()
    {
        var (viewModel, settings) = CreateViewModel(["#FF0000"]);

        viewModel.RemoveFavoriteCommand.Execute("#00FF00");

        Assert.Single(viewModel.FavoriteColors);
        Assert.Equal(0, settings.UpdateCallCount);
    }

    [Fact]
    public void AutoStartOpenRgb_Toggled_PersistsThroughSettingsStore()
    {
        var (viewModel, settings) = CreateViewModel();

        viewModel.AutoStartOpenRgb = true;

        Assert.True(settings.Current.Lighting.AutoStartOpenRgb);
        Assert.Equal(1, settings.UpdateCallCount);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var (viewModel, _) = CreateViewModel();

        viewModel.Dispose();
        var exception = Record.Exception(viewModel.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public async Task OnNavigatedToAsync_ConflictsDetected_PopulatesConflictsAndShowsPanel()
    {
        var (viewModel, _, _, conflictDetector, _, _) = CreateViewModelWithDependencies();
        conflictDetector.Warnings = [new LightingConflictWarning("windows-dynamic-lighting", "Windows Dynamic Lighting", "message")];

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Single(viewModel.Conflicts);
        Assert.True(viewModel.ShowConflicts);
    }

    [Fact]
    public async Task OnNavigatedToAsync_NoConflicts_PanelHidden()
    {
        var (viewModel, _, _, _, _, _) = CreateViewModelWithDependencies();

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Empty(viewModel.Conflicts);
        Assert.False(viewModel.ShowConflicts);
    }

    [Fact]
    public async Task DismissConflicts_HidesPanelWithoutClearingTheList()
    {
        var (viewModel, _, _, conflictDetector, _, _) = CreateViewModelWithDependencies();
        conflictDetector.Warnings = [new LightingConflictWarning("vendor-lghub", "Logitech G HUB", "message")];
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        viewModel.DismissConflictsCommand.Execute(null);

        Assert.False(viewModel.ShowConflicts);
        Assert.Single(viewModel.Conflicts);
    }

    [Fact]
    public void OpenConflictAction_WarningHasActionUri_OpensItThroughUrlLauncher()
    {
        var (viewModel, _, _, _, urlLauncher, _) = CreateViewModelWithDependencies();
        var warning = new LightingConflictWarning(
            "windows-dynamic-lighting", "Windows Dynamic Lighting", "message", "ms-settings:personalization-lighting");

        viewModel.OpenConflictActionCommand.Execute(warning);

        Assert.Equal(["ms-settings:personalization-lighting"], urlLauncher.OpenedUrls);
    }

    [Fact]
    public void OpenConflictAction_WarningHasNoActionUri_DoesNotThrowOrOpenAnything()
    {
        var (viewModel, _, _, _, urlLauncher, _) = CreateViewModelWithDependencies();
        var warning = new LightingConflictWarning("vendor-icue", "Corsair iCUE", "message");

        var exception = Record.Exception(() => viewModel.OpenConflictActionCommand.Execute(warning));

        Assert.Null(exception);
        Assert.Empty(urlLauncher.OpenedUrls);
    }

    [Fact]
    public async Task RefreshAfterConnect_DeviceExcludedFromSettings_DeviceRowStartsExcluded()
    {
        var (viewModel, settings, lightingService, _, _, componentService) = CreateViewModelWithDependencies();
        settings.Current.Lighting.ExcludedDeviceNames = ["Keyboard"];
        componentService.SetStatus(ComponentIds.OpenRgb, new ComponentStatus(ComponentState.Running));
        lightingService.ConnectResult = true;
        lightingService.Devices = [new RgbDevice(0, "Keyboard", RgbDeviceType.Keyboard, "Vendor", [], "Direct", 1, [])];

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        var device = Assert.Single(viewModel.Devices);
        Assert.True(device.IsExcluded);
    }

    [Fact]
    public async Task ToggleDeviceExclusion_PersistsToSettings()
    {
        var (viewModel, settings, lightingService, _, _, componentService) = CreateViewModelWithDependencies();
        componentService.SetStatus(ComponentIds.OpenRgb, new ComponentStatus(ComponentState.Running));
        lightingService.ConnectResult = true;
        lightingService.Devices = [new RgbDevice(0, "Keyboard", RgbDeviceType.Keyboard, "Vendor", [], "Direct", 1, [])];
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        var device = Assert.Single(viewModel.Devices);
        var updateCallsBefore = settings.UpdateCallCount;

        device.IsExcluded = true;

        Assert.Contains("Keyboard", settings.Current.Lighting.ExcludedDeviceNames);
        Assert.True(settings.UpdateCallCount > updateCallsBefore);

        device.IsExcluded = false;

        Assert.DoesNotContain("Keyboard", settings.Current.Lighting.ExcludedDeviceNames);
    }
}
