using CommunityToolkit.Mvvm.Input;
using Porchlight.App.Controls;
using Porchlight.App.Features.Setup;
using Porchlight.App.Shell;
using Porchlight.Core.Components;

namespace Porchlight.App.Features.Settings;

/// <summary>The Settings > Optional features page: one shared <see cref="ComponentCardViewModel"/>
/// per optional component (AnyDesk, OpenRGB, PawnIO) with install/start/retry and live status, plus a
/// button that re-opens the first-run setup wizard.</summary>
public sealed partial class OptionalFeaturesViewModel : PageViewModelBase, IDisposable
{
    private readonly ISetupLauncher _setupLauncher;
    private bool _disposed;

    public OptionalFeaturesViewModel(IComponentCardViewModelFactory cardFactory, ISetupLauncher setupLauncher)
    {
        _setupLauncher = setupLauncher;
        Cards = ComponentCatalog.All.Select(d => cardFactory.Create(d.Id)).ToList();
    }

    public override string Title => "Optional features";

    // Segoe Fluent Icons "Puzzle".
    public override string Glyph => "";

    public override int Order => 2;

    public override PageCategory Category => PageCategory.Settings;

    /// <summary>One card per optional component, in catalog order.</summary>
    public IReadOnlyList<ComponentCardViewModel> Cards { get; }

    /// <summary>Re-checks every idle component, so a tool installed or removed outside Porchlight
    /// shows correctly.</summary>
    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        foreach (var card in Cards.Where(c => !c.IsBusy))
        {
            await card.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void RunFirstTimeSetup() => _setupLauncher.ShowSetup();

    /// <summary>Idempotent (see <see cref="PageServiceCollectionExtensions.AddPage{TViewModel, TView}"/>):
    /// the cards come from the factory, so the container never disposes them.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var card in Cards)
        {
            card.Dispose();
        }
    }
}
