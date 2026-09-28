using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Setup;
using Porchlight.Core.Components;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Features.Setup;

public sealed class SetupViewModelTests : IDisposable
{
    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeComponentService _componentService = new();
    private readonly FakeRegistryReader _registryReader = new();

    public SetupViewModelTests()
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

    private SetupViewModel CreateViewModel() =>
        new(_componentService, _registryReader, _settingsStore, NullLogger<SetupViewModel>.Instance);

    [Fact]
    public void MarkFirstRunCompleted_PersistsToSettings()
    {
        Assert.False(_settingsStore.Current.Setup.FirstRunCompleted);
        var viewModel = CreateViewModel();

        viewModel.MarkFirstRunCompleted();

        Assert.True(_settingsStore.Current.Setup.FirstRunCompleted);
    }

    [Fact]
    public void MarkFirstRunCompleted_CalledTwice_StaysTrue()
    {
        var viewModel = CreateViewModel();

        viewModel.MarkFirstRunCompleted();
        viewModel.MarkFirstRunCompleted();

        Assert.True(_settingsStore.Current.Setup.FirstRunCompleted);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // SetupWindow's Closed handler disposes the view model, then the DI container disposes it
        // again on host shutdown.
        var viewModel = CreateViewModel();

        viewModel.Dispose();
        var ex = Record.Exception(viewModel.Dispose);

        Assert.Null(ex);
    }

    [Fact]
    public async Task LoadAsync_InstallerHandledComponent_DefaultsToUnticked()
    {
        _registryReader.InstallerHandledComponentIds = [ComponentIds.AnyDesk];
        var viewModel = CreateViewModel();

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var anyDeskItem = Assert.Single(viewModel.Items, i => i.Definition.Id == ComponentIds.AnyDesk);
        Assert.False(anyDeskItem.IsSelected);
        // Still re-detected for real, independent of the installer marker.
        Assert.Equal(ComponentState.NotInstalled, anyDeskItem.Status.State);
    }

    [Fact]
    public async Task LoadAsync_NotHandledByInstallerAndNotInstalled_OnlyAnyDeskDefaultsToTicked()
    {
        var viewModel = CreateViewModel();

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var anyDeskItem = Assert.Single(viewModel.Items, i => i.Definition.Id == ComponentIds.AnyDesk);
        var openRgbItem = Assert.Single(viewModel.Items, i => i.Definition.Id == ComponentIds.OpenRgb);
        var pawnIoItem = Assert.Single(viewModel.Items, i => i.Definition.Id == ComponentIds.PawnIo);
        Assert.True(anyDeskItem.IsSelected);
        Assert.False(openRgbItem.IsSelected);
        Assert.False(pawnIoItem.IsSelected);
    }

    [Fact]
    public async Task LoadAsync_AnyDeskInstallerHandled_DefaultsToUnticked()
    {
        _registryReader.InstallerHandledComponentIds = [ComponentIds.AnyDesk];
        var viewModel = CreateViewModel();

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var anyDeskItem = Assert.Single(viewModel.Items, i => i.Definition.Id == ComponentIds.AnyDesk);
        Assert.False(anyDeskItem.IsSelected);
    }

    [Fact]
    public async Task LoadAsync_OpenRgbAndPawnIoNotHandledByInstaller_StillDefaultToUnticked()
    {
        // OpenRGB and PawnIO are opt-in regardless of the installer marker - only AnyDesk is
        // pre-ticked by default (docs/specs/01b-components.md).
        var viewModel = CreateViewModel();

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var openRgbItem = Assert.Single(viewModel.Items, i => i.Definition.Id == ComponentIds.OpenRgb);
        var pawnIoItem = Assert.Single(viewModel.Items, i => i.Definition.Id == ComponentIds.PawnIo);
        Assert.False(openRgbItem.IsSelected);
        Assert.False(pawnIoItem.IsSelected);
    }

    [Fact]
    public async Task LoadAsync_AlreadyInstalled_DefaultsToUnticked()
    {
        _componentService.SetStatus(ComponentIds.OpenRgb, new ComponentStatus(ComponentState.Installed, "1.0.0"));
        var viewModel = CreateViewModel();

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var openRgbItem = Assert.Single(viewModel.Items, i => i.Definition.Id == ComponentIds.OpenRgb);
        Assert.False(openRgbItem.IsSelected);
        Assert.True(openRgbItem.IsAlreadyInstalled);
    }

    [Fact]
    public async Task PrimaryButtonText_BeforeAnyRun_IsSetUp()
    {
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Set up", viewModel.PrimaryButtonText);
    }

    [Fact]
    public async Task PrimaryAction_AllSucceed_SwitchesToCloseAndMarksFirstRunCompleted()
    {
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        foreach (var id in new[] { ComponentIds.AnyDesk, ComponentIds.OpenRgb, ComponentIds.PawnIo })
        {
            _componentService.QueueInstallResult(id, new ComponentStatus(ComponentState.Installed));
        }

        await viewModel.PrimaryActionCommand.ExecuteAsync(null);

        Assert.Equal("Close", viewModel.PrimaryButtonText);
        Assert.True(_settingsStore.Current.Setup.FirstRunCompleted);
        Assert.False(viewModel.ShowSkip);
    }

    [Fact]
    public async Task PrimaryAction_OneFails_SwitchesToRetryNotClose()
    {
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        _componentService.QueueInstallResult(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Error, Message: "boom"));
        _componentService.QueueInstallResult(ComponentIds.OpenRgb, new ComponentStatus(ComponentState.Installed));
        _componentService.QueueInstallResult(ComponentIds.PawnIo, new ComponentStatus(ComponentState.Installed));

        await viewModel.PrimaryActionCommand.ExecuteAsync(null);

        Assert.Equal("Retry", viewModel.PrimaryButtonText);
    }

    [Fact]
    public async Task PrimaryAction_Retry_ReinstallsOnlyTheFailedItem()
    {
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        _componentService.QueueInstallResult(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Error, Message: "boom"));
        _componentService.QueueInstallResult(ComponentIds.OpenRgb, new ComponentStatus(ComponentState.Installed));
        _componentService.QueueInstallResult(ComponentIds.PawnIo, new ComponentStatus(ComponentState.Installed));
        await viewModel.PrimaryActionCommand.ExecuteAsync(null);
        _componentService.InstallCalls.Clear();
        _componentService.QueueInstallResult(ComponentIds.AnyDesk, new ComponentStatus(ComponentState.Installed));

        await viewModel.PrimaryActionCommand.ExecuteAsync(null);

        Assert.Equal([ComponentIds.AnyDesk], _componentService.InstallCalls);
        Assert.Equal("Close", viewModel.PrimaryButtonText);
    }
}
