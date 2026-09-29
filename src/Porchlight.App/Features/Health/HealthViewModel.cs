using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porchlight.App.Shell;
using Porchlight.Core.Elevation;

namespace Porchlight.App.Features.Health;

/// <summary>The "Health check" page: five cards, each loading (and failing) independently.</summary>
public sealed partial class HealthViewModel : PageViewModelBase, IBusyGuard
{
    private readonly IElevationService _elevation;
    private bool _loadedOnce;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isRefreshing;

    public HealthViewModel(
        IElevationService elevation,
        DiskHealthCardViewModel disks,
        RepairCardViewModel repair,
        RestorePointCardViewModel restorePoint,
        ProblemsCardViewModel problems,
        BatteryCardViewModel battery)
    {
        _elevation = elevation;
        Disks = disks;
        Repair = repair;
        RestorePoint = restorePoint;
        Problems = problems;
        Battery = battery;
    }

    public override string Title => "Health check";

    public override string Glyph => "";

    public override int Order => 4;

    public override PageCategory Category => PageCategory.TuneUp;

    public DiskHealthCardViewModel Disks { get; }

    public RepairCardViewModel Repair { get; }

    public RestorePointCardViewModel RestorePoint { get; }

    public ProblemsCardViewModel Problems { get; }

    public BatteryCardViewModel Battery { get; }

    public bool ShowAdminBanner => !_elevation.IsElevated;

    public bool IsBusyWithWork => Repair.IsRunning || RestorePoint.IsCreating;

    public string BusyMessage => Repair.IsRunning
        ? "Windows is being checked or repaired. Closing Porchlight won't stop it, but you won't see the result. Close anyway?"
        : "A restore point is being created. Closing Porchlight won't stop it. Close anyway?";

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        if (_loadedOnce)
        {
            return;
        }

        _loadedOnce = true;
        await RefreshAsync(cancellationToken);
    }

    private bool CanRefresh() => !IsRefreshing;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        try
        {
            await Task.WhenAll(
                Disks.RefreshAsync(cancellationToken),
                RestorePoint.RefreshAsync(cancellationToken),
                Problems.RefreshAsync(cancellationToken),
                Battery.RefreshAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Navigated away / shutting down: nothing to show.
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
