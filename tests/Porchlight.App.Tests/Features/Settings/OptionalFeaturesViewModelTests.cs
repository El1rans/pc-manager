using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Controls;
using Porchlight.App.Features.Settings;
using Porchlight.App.Features.Setup;
using Porchlight.App.Shell;
using Porchlight.App.Tests.Features.Setup;
using Porchlight.Core.Components;
using Xunit;

namespace Porchlight.App.Tests.Features.Settings;

public sealed class OptionalFeaturesViewModelTests
{
    [Fact]
    public void HasOneCardPerCatalogComponent_InCatalogOrder()
    {
        var (viewModel, _, _) = Create();

        Assert.Equal(
            [ComponentIds.AnyDesk, ComponentIds.OpenRgb, ComponentIds.PawnIo],
            viewModel.Cards.Select(c => c.Definition.Id));
    }

    [Fact]
    public void IsAPageInTheSettingsCategory_ThirdTab()
    {
        var (viewModel, _, _) = Create();

        Assert.Equal(PageCategory.Settings, viewModel.Category);
        Assert.Equal("Optional features", viewModel.TabTitle);
        Assert.Equal(2, viewModel.Order);
    }

    [Fact]
    public async Task NavigatingToThePage_RefreshesEveryCardsStatus()
    {
        var (viewModel, components, _) = Create();
        components.SetStatus(ComponentIds.PawnIo, new ComponentStatus(ComponentState.Installed));

        await viewModel.OnNavigatedToAsync(CancellationToken.None);

        Assert.All(viewModel.Cards, c => Assert.False(c.IsLoading));
        Assert.True(viewModel.Cards.Single(c => c.Definition.Id == ComponentIds.PawnIo).IsReady);
        Assert.False(viewModel.Cards.Single(c => c.Definition.Id == ComponentIds.AnyDesk).IsReady);
    }

    [Fact]
    public void RunFirstTimeSetupCommand_OpensTheSetupWizard()
    {
        var (viewModel, _, setup) = Create();

        viewModel.RunFirstTimeSetupCommand.Execute(null);

        Assert.Equal(1, setup.ShowCount);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow_AndReleasesTheCards()
    {
        var (viewModel, components, _) = Create();
        Assert.Equal(3, components.StatusChangedSubscriberCount);

        viewModel.Dispose();
        var exception = Record.Exception(viewModel.Dispose);

        Assert.Null(exception);
        Assert.Equal(0, components.StatusChangedSubscriberCount);
    }

    private static (OptionalFeaturesViewModel ViewModel, FakeComponentService Components, FakeSetupLauncher Setup) Create()
    {
        var components = new FakeComponentService();
        var setup = new FakeSetupLauncher();
        var factory = new ComponentCardViewModelFactory(components, NullLoggerFactory.Instance);
        return (new OptionalFeaturesViewModel(factory, setup), components, setup);
    }

    private sealed class FakeSetupLauncher : ISetupLauncher
    {
        public int ShowCount { get; private set; }

        public void ShowSetup() => ShowCount++;
    }
}
