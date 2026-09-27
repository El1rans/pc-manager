using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PCManager.App.Features.Setup;

namespace PCManager.App.Shell;

/// <summary>View model for <see cref="MainWindow"/>: navigation rail and admin status.</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IShellService _shellService;
    private readonly IPageViewLocator _pageViewLocator;
    private readonly ISetupLauncher _setupLauncher;
    private readonly ILogger<MainViewModel> _logger;
    private CancellationTokenSource _navigationCts = new();
    private Task _currentNavigation = Task.CompletedTask;

    [ObservableProperty]
    private IPage? _selectedPage;

    [ObservableProperty]
    private FrameworkElement? _selectedPageView;

    public MainViewModel(
        IEnumerable<IPage> pages,
        IShellService shellService,
        IPageViewLocator pageViewLocator,
        ISetupLauncher setupLauncher,
        ILogger<MainViewModel> logger)
    {
        _shellService = shellService;
        _pageViewLocator = pageViewLocator;
        _setupLauncher = setupLauncher;
        _logger = logger;
        var pageList = pages as IReadOnlyCollection<IPage> ?? pages.ToList();
        Pages = new ObservableCollection<IPage>(pageList.Where(p => !p.IsPinnedToBottom).OrderBy(p => p.Order));
        PinnedPages = new ObservableCollection<IPage>(pageList.Where(p => p.IsPinnedToBottom).OrderBy(p => p.Order));
    }

    public ObservableCollection<IPage> Pages { get; }

    /// <summary>Pages shown in their own group at the bottom of the nav rail - see
    /// <see cref="IPage.IsPinnedToBottom"/>.</summary>
    public ObservableCollection<IPage> PinnedPages { get; }

    public bool IsElevated => _shellService.IsElevated;

    public string AdminStatusText => IsElevated ? "Running as administrator" : "Not running as administrator";

    public IRelayCommand RestartElevatedCommand => _shellService.RestartElevatedCommand;

    [RelayCommand]
    private void OpenSetup() => _setupLauncher.ShowSetup();

    /// <summary>
    /// Selects and awaits the load of the first page. Called from <see cref="MainWindow"/>'s
    /// Loaded event rather than the constructor, so DI resolution never triggers page work.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectedPage = Pages.FirstOrDefault();
        await _currentNavigation.ConfigureAwait(true);
    }

    /// <summary>
    /// Every navigation - the first one included - cancels whichever page load was in flight,
    /// swaps in the newly resolved view, and tells the newly selected page to (re)load.
    /// </summary>
    partial void OnSelectedPageChanged(IPage? value)
    {
        SelectedPageView = value is null ? null : _pageViewLocator.GetOrCreateView(value);

        _navigationCts.Cancel();
        _navigationCts.Dispose();
        _navigationCts = new CancellationTokenSource();

        _currentNavigation = NavigateToAsync(value, _navigationCts.Token);
    }

    /// <summary>
    /// Awaits the page's load and handles every outcome internally (cancellation is expected and
    /// silent; anything else is logged) so callers can safely fire-and-forget this method without
    /// ever discarding a fault.
    /// </summary>
    private async Task NavigateToAsync(IPage? page, CancellationToken cancellationToken)
    {
        if (page is null)
        {
            return;
        }

        try
        {
            await page.OnNavigatedToAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Navigating away cancels the in-flight load; expected, not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Page {PageTitle} failed to load.", page.Title);
        }
    }

    public void Dispose()
    {
        _navigationCts.Cancel();
        _navigationCts.Dispose();
    }
}
