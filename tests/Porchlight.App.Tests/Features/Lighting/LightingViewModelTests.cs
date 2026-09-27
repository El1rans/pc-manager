using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Controls;
using Porchlight.App.Tests.Features.Setup;
using Xunit;

namespace Porchlight.App.Tests.Features.Lighting;

public sealed class LightingViewModelTests
{
    private static (Porchlight.App.Features.Lighting.LightingViewModel ViewModel, FakeSettingsStore Settings) CreateViewModel(
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

        var viewModel = new Porchlight.App.Features.Lighting.LightingViewModel(
            factory, lightingService, settings, NullLoggerFactory.Instance);

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
}
