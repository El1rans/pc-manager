using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Elevation;

namespace PCManager.App.Shell;

/// <summary>View model for <see cref="MainWindow"/>: navigation rail and admin status.</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IElevationService _elevationService;
    private CancellationTokenSource _navigationCts = new();

    [ObservableProperty]
    private IPage? _selectedPage;

    public MainViewModel(IEnumerable<IPage> pages, IElevationService elevationService)
    {
        _elevationService = elevationService;
        Pages = new ObservableCollection<IPage>(pages.OrderBy(p => p.Order));
        SelectedPage = Pages.FirstOrDefault();
    }

    public ObservableCollection<IPage> Pages { get; }

    public bool IsElevated => _elevationService.IsElevated;

    public string AdminStatusText => IsElevated ? "Running as administrator" : "Not running as administrator";

    [RelayCommand(CanExecute = nameof(CanRestartElevated))]
    private void RestartElevated()
    {
        _elevationService.RestartElevated();
    }

    private bool CanRestartElevated() => !IsElevated;

    /// <summary>
    /// Every navigation - the first one included - cancels whichever page load was in flight and
    /// tells the newly selected page to (re)load.
    /// </summary>
    partial void OnSelectedPageChanged(IPage? value)
    {
        _navigationCts.Cancel();
        _navigationCts.Dispose();
        _navigationCts = new CancellationTokenSource();

        if (value is not null)
        {
            _ = value.OnNavigatedToAsync(_navigationCts.Token);
        }
    }

    public void Dispose()
    {
        _navigationCts.Cancel();
        _navigationCts.Dispose();
    }
}
