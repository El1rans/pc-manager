using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Controls;
using Porchlight.App.Tests.Features.RemoteSupport;
using Porchlight.App.Tests.Features.Setup;
using Porchlight.Core.Components;
using Porchlight.Core.Hardware;
using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;
using Porchlight.Core.Lighting.Effects.CustomAnimations;
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
        IEnumerable<string>? seedFavorites = null) =>
        CreateViewModelWithDependencies(
            seedFavorites, new FakeCustomAnimationLibrary(), new FakeClipboardService(), new FakeAnimationFilePicker());

    private static (
        Porchlight.App.Features.Lighting.LightingViewModel ViewModel,
        FakeSettingsStore Settings,
        FakeLightingService LightingService,
        FakeLightingConflictDetector ConflictDetector,
        FakeUrlLauncher UrlLauncher,
        FakeComponentService ComponentService) CreateViewModelWithDependencies(
        IEnumerable<string>? seedFavorites,
        FakeCustomAnimationLibrary customAnimations,
        FakeClipboardService clipboard,
        FakeAnimationFilePicker filePicker)
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
        var effectEngine = new EffectEngine(
            new FakeEffectDeviceClient(),
            new FakeHardwareService(),
            new ZeroPendingUpdateCountProvider(),
            new SettingsDeviceExclusionProvider(settings),
            new NullKeyPressSource(),
            NullLogger<EffectEngine>.Instance);

        var viewModel = new Porchlight.App.Features.Lighting.LightingViewModel(
            factory,
            lightingService,
            conflictDetector,
            urlLauncher,
            settings,
            effectEngine,
            customAnimations,
            clipboard,
            filePicker,
            NullLoggerFactory.Instance);

        return (viewModel, settings, lightingService, conflictDetector, urlLauncher, componentService);
    }

    /// <summary>Minimal <see cref="IEffectDeviceClient"/> that never actually connects - the LED
    /// effects engine these tests wire up is never expected to start (no test here assigns an
    /// effect), but <see cref="Porchlight.App.Features.Lighting.LightingViewModel"/> still needs a
    /// real <see cref="EffectEngine"/> instance to construct.</summary>
    private sealed class FakeEffectDeviceClient : IEffectDeviceClient
    {
        public bool Connected => false;

        public void Connect()
        {
        }

        public IReadOnlyList<EffectDeviceInfo> GetAllDevices() => [];

        public void SetMode(int deviceIndex, int modeIndex)
        {
        }

        public void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeHardwareService : IHardwareService
    {
        public event EventHandler<HardwareSnapshot>? SnapshotUpdated
        {
            add { }
            remove { }
        }

        public HardwareSnapshot Latest { get; } = HardwareSnapshot.Empty(HardwareStatus.NotElevated);

        public IReadOnlyList<IFanController> Controllers { get; } = [];

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void ResetMinMax()
        {
        }

        public void RunOnOwnerThread(Action action, TimeSpan timeout, bool allowDirectFallback = true) => action();
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
    public async Task RefreshAfterConnect_AssignsEffectFromSettings_DeviceRowLoadsIt()
    {
        var (viewModel, settings, lightingService, _, _, componentService) = CreateViewModelWithDependencies();
        settings.Current.Lighting.EffectAssignments =
        [
            new Porchlight.Core.Lighting.Effects.EffectAssignment { DeviceKey = "Keyboard", EffectName = "Rainbow wave" },
        ];
        componentService.SetStatus(ComponentIds.OpenRgb, new ComponentStatus(ComponentState.Running));
        lightingService.ConnectResult = true;
        lightingService.Devices = [new RgbDevice(0, "Keyboard", RgbDeviceType.Keyboard, "Vendor", [], "Direct", 1, [])];

        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        var device = Assert.Single(viewModel.Devices);
        Assert.Equal("Rainbow wave", device.SelectedEffectName);
    }

    [Fact]
    public async Task SelectingAnEffect_PersistsAssignmentToSettings()
    {
        var (viewModel, settings, lightingService, _, _, componentService) = CreateViewModelWithDependencies();
        componentService.SetStatus(ComponentIds.OpenRgb, new ComponentStatus(ComponentState.Running));
        lightingService.ConnectResult = true;
        lightingService.Devices = [new RgbDevice(0, "Keyboard", RgbDeviceType.Keyboard, "Vendor", [], "Direct", 1, [])];
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        var device = Assert.Single(viewModel.Devices);

        device.SelectedEffectName = "Breathing";

        var saved = Assert.Single(settings.Current.Lighting.EffectAssignments);
        Assert.Equal("Keyboard", saved.DeviceKey);
        Assert.Equal("Breathing", saved.EffectName);
    }

    [Fact]
    public async Task SelectingNone_RemovesAssignmentFromSettings()
    {
        var (viewModel, settings, lightingService, _, _, componentService) = CreateViewModelWithDependencies();
        componentService.SetStatus(ComponentIds.OpenRgb, new ComponentStatus(ComponentState.Running));
        lightingService.ConnectResult = true;
        lightingService.Devices = [new RgbDevice(0, "Keyboard", RgbDeviceType.Keyboard, "Vendor", [], "Direct", 1, [])];
        await viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        var device = Assert.Single(viewModel.Devices);
        device.SelectedEffectName = "Breathing";
        Assert.Single(settings.Current.Lighting.EffectAssignments);

        device.SelectedEffectName = "None";

        Assert.Empty(settings.Current.Lighting.EffectAssignments);
    }

    [Fact]
    public void ToggleEffectsPaused_FlipsEffectsPausedAndLabel()
    {
        var (viewModel, _) = CreateViewModel();

        Assert.False(viewModel.EffectsPaused);
        Assert.Equal("Pause effects", viewModel.PauseEffectsButtonLabel);

        viewModel.ToggleEffectsPausedCommand.Execute(null);

        Assert.True(viewModel.EffectsPaused);
        Assert.Equal("Resume effects", viewModel.PauseEffectsButtonLabel);

        viewModel.ToggleEffectsPausedCommand.Execute(null);

        Assert.False(viewModel.EffectsPaused);
        Assert.Equal("Pause effects", viewModel.PauseEffectsButtonLabel);
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

    private const string PoliceLights = """
        { "name": "Police lights", "frames": [ { "fill": "#FF0000" }, { "fill": "#0000FF" } ] }
        """;

    private const string Sunset = """
        { "name": "Sunset", "frames": [ { "gradient": ["#FF7A00", "#7A2BFF"] } ] }
        """;

    private sealed record AnimationFixture(
        Porchlight.App.Features.Lighting.LightingViewModel ViewModel,
        FakeSettingsStore Settings,
        FakeLightingService LightingService,
        FakeComponentService ComponentService,
        FakeCustomAnimationLibrary Library,
        FakeClipboardService Clipboard,
        FakeAnimationFilePicker FilePicker);

    private static AnimationFixture CreateAnimationFixture(params string[] preImported)
    {
        var library = new FakeCustomAnimationLibrary();
        foreach (var json in preImported)
        {
            Assert.True(library.ImportText(json).Succeeded);
        }

        var clipboard = new FakeClipboardService();
        var filePicker = new FakeAnimationFilePicker();
        var (viewModel, settings, lightingService, _, _, componentService) =
            CreateViewModelWithDependencies(null, library, clipboard, filePicker);
        return new AnimationFixture(viewModel, settings, lightingService, componentService, library, clipboard, filePicker);
    }

    private static async Task<Porchlight.App.Features.Lighting.DeviceRowViewModel> ConnectOneDeviceAsync(AnimationFixture fixture)
    {
        fixture.ComponentService.SetStatus(ComponentIds.OpenRgb, new ComponentStatus(ComponentState.Running));
        fixture.LightingService.ConnectResult = true;
        fixture.LightingService.Devices = [new RgbDevice(0, "Keyboard", RgbDeviceType.Keyboard, "Vendor", [], "Direct", 1, [])];
        await fixture.ViewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        return Assert.Single(fixture.ViewModel.Devices);
    }

    [Fact]
    public void Construction_ListsAlreadyImportedAnimationsByName()
    {
        var fixture = CreateAnimationFixture(Sunset, PoliceLights);

        Assert.Equal(["Police lights", "Sunset"], fixture.ViewModel.CustomAnimations.Select(a => a.Name));
    }

    [Fact]
    public void CopyAiPrompt_PutsPromptTemplateOnClipboard()
    {
        var fixture = CreateAnimationFixture();

        fixture.ViewModel.CopyAiPromptCommand.Execute(null);

        Assert.Equal(CustomAnimationPrompt.Template, fixture.Clipboard.LastText);
        Assert.StartsWith("AI prompt copied", fixture.ViewModel.AnimationStatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void CopyAiPrompt_ClipboardBusy_SaysSo()
    {
        var fixture = CreateAnimationFixture();
        fixture.Clipboard.NextResult = false;

        fixture.ViewModel.CopyAiPromptCommand.Execute(null);

        Assert.Equal("Couldn't copy the prompt. Please try again.", fixture.ViewModel.AnimationStatusMessage);
    }

    [Fact]
    public void PasteAnimation_ValidAnswer_ImportsAndListsIt()
    {
        var fixture = CreateAnimationFixture();
        fixture.Clipboard.ClipboardText = "```json\n" + PoliceLights + "\n```";

        fixture.ViewModel.PasteAnimationCommand.Execute(null);

        var listed = Assert.Single(fixture.ViewModel.CustomAnimations);
        Assert.Equal("police-lights", listed.Id);
        Assert.StartsWith("Imported \"Police lights\"", fixture.ViewModel.AnimationStatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PasteAnimation_InvalidAnswer_ShowsParserErrorAndImportsNothing()
    {
        var fixture = CreateAnimationFixture();
        fixture.Clipboard.ClipboardText = """{ "name": "Broken" }""";

        fixture.ViewModel.PasteAnimationCommand.Execute(null);

        Assert.Empty(fixture.ViewModel.CustomAnimations);
        Assert.Contains("\"frames\" is required", fixture.ViewModel.AnimationStatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PasteAnimation_EmptyClipboard_AsksToCopyFirst()
    {
        var fixture = CreateAnimationFixture();

        fixture.ViewModel.PasteAnimationCommand.Execute(null);

        Assert.StartsWith("The clipboard is empty", fixture.ViewModel.AnimationStatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportAnimationFile_Cancelled_DoesNothing()
    {
        var fixture = CreateAnimationFixture();

        fixture.ViewModel.ImportAnimationFileCommand.Execute(null);

        Assert.Empty(fixture.ViewModel.CustomAnimations);
        Assert.Null(fixture.ViewModel.AnimationStatusMessage);
    }

    [Fact]
    public void ImportAnimationFile_PickedFile_Imports()
    {
        var fixture = CreateAnimationFixture();
        fixture.Library.Files["C:\\Downloads\\sunset.json"] = Sunset;
        fixture.FilePicker.NextPath = "C:\\Downloads\\sunset.json";

        fixture.ViewModel.ImportAnimationFileCommand.Execute(null);

        Assert.Equal("Sunset", Assert.Single(fixture.ViewModel.CustomAnimations).Name);
    }

    [Fact]
    public async Task SelectingCustomAnimation_DefaultsToFirstAnimationAndPersistsIt()
    {
        var fixture = CreateAnimationFixture(PoliceLights, Sunset);
        var device = await ConnectOneDeviceAsync(fixture);

        device.SelectedEffectName = EffectRegistry.CustomAnimationEffectName;

        Assert.Equal("police-lights", device.SelectedCustomAnimationId);
        var saved = Assert.Single(fixture.Settings.Current.Lighting.EffectAssignments);
        Assert.Equal(EffectRegistry.CustomAnimationEffectName, saved.EffectName);
        Assert.Equal("police-lights", saved.Settings[EffectRegistry.CustomAnimationIdSetting]);

        device.SelectedCustomAnimationId = "sunset";

        Assert.Equal("sunset", Assert.Single(fixture.Settings.Current.Lighting.EffectAssignments)
            .Settings[EffectRegistry.CustomAnimationIdSetting]);
    }

    [Fact]
    public async Task RemoveAnimation_InUse_ClearsDeviceSelectionAndPersists()
    {
        var fixture = CreateAnimationFixture(PoliceLights);
        var device = await ConnectOneDeviceAsync(fixture);
        device.SelectedEffectName = EffectRegistry.CustomAnimationEffectName;

        fixture.ViewModel.RemoveAnimationCommand.Execute(fixture.ViewModel.CustomAnimations[0]);

        Assert.Empty(fixture.ViewModel.CustomAnimations);
        Assert.Null(device.SelectedCustomAnimationId);
        var saved = Assert.Single(fixture.Settings.Current.Lighting.EffectAssignments);
        Assert.False(saved.Settings.ContainsKey(EffectRegistry.CustomAnimationIdSetting));
        Assert.Equal("Removed \"Police lights\".", fixture.ViewModel.AnimationStatusMessage);
    }

    [Fact]
    public async Task Reimport_SameName_KeepsDeviceSelection()
    {
        var fixture = CreateAnimationFixture(PoliceLights);
        var device = await ConnectOneDeviceAsync(fixture);
        device.SelectedEffectName = EffectRegistry.CustomAnimationEffectName;
        fixture.Clipboard.ClipboardText = """
            { "name": "Police lights", "frames": [ { "fill": "#FFFFFF" } ] }
            """;

        fixture.ViewModel.PasteAnimationCommand.Execute(null);

        Assert.Equal(1, Assert.Single(fixture.ViewModel.CustomAnimations).FrameCount);
        Assert.Equal("police-lights", device.SelectedCustomAnimationId);
    }
}
