using Porchlight.App.Features.Settings;
using Porchlight.App.Shell;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Features.Settings;

public sealed class TextSizeViewModelTests
{
    [Fact]
    public void DefaultsToNormal_AndOffersThreeChoices()
    {
        var viewModel = new TextSizeViewModel(new FakeSettingsStore(), new RecordingScale());

        Assert.Equal(TextSize.Normal, viewModel.SelectedSize.Value);
        Assert.Equal(["Normal", "Large", "Extra large"], viewModel.Options.Select(o => o.Label));
    }

    [Fact]
    public void SavedSize_IsSelectedOnOpen_WithoutApplyingOrSaving()
    {
        var store = new FakeSettingsStore();
        store.Current.Appearance.TextSize = TextSize.Large;
        var scale = new RecordingScale();

        var viewModel = new TextSizeViewModel(store, scale);

        Assert.Equal(TextSize.Large, viewModel.SelectedSize.Value);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Empty(scale.Applied);
    }

    [Fact]
    public void ChoosingASize_PersistsAndAppliesItImmediately()
    {
        var store = new FakeSettingsStore();
        var scale = new RecordingScale();
        var viewModel = new TextSizeViewModel(store, scale);

        viewModel.SelectedSize = viewModel.Options.Single(o => o.Value == TextSize.Large);
        viewModel.SelectedSize = viewModel.Options.Single(o => o.Value == TextSize.ExtraLarge);

        Assert.Equal(TextSize.ExtraLarge, store.Current.Appearance.TextSize);
        Assert.Equal([TextSize.Large, TextSize.ExtraLarge], scale.Applied);
    }

    private sealed class RecordingScale : ITextScaleService
    {
        public List<TextSize> Applied { get; } = [];

        public void Apply(TextSize size) => Applied.Add(size);
    }
}
