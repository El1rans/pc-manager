using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Porchlight.App.Shell;

/// <summary>View model for <see cref="MainWindow"/>: navigation rail and admin status.</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IShellService _shellService;
    private readonly IPageViewLocator _pageViewLocator;
    private readonly IPageNavigator _navigator;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IReadOnlyList<NavCategoryViewModel> _allCategories;
    private CancellationTokenSource _navigationCts = new();
    private bool _disposed;
    private Task _currentNavigation = Task.CompletedTask;

    [ObservableProperty]
    private IPage? _selectedPage;

    [ObservableProperty]
    private NavCategoryViewModel? _selectedCategory;

    [ObservableProperty]
    private FrameworkElement? _selectedPageView;

    public MainViewModel(
        IEnumerable<IPage> pages,
        IShellService shellService,
        IPageViewLocator pageViewLocator,
        IPageNavigator navigator,
        ILogger<MainViewModel> logger)
    {
        _shellService = shellService;
        _pageViewLocator = pageViewLocator;
        _navigator = navigator;
        _logger = logger;
        var pageList = pages as IReadOnlyCollection<IPage> ?? pages.ToList();
        var categories = pageList
            .GroupBy(p => p.Category)
            .Select(g => new NavCategoryViewModel(PageCategoryCatalog.Get(g.Key), g))
            .OrderBy(c => c.Info.Order)
            .ToList();
        _allCategories = categories;
        Categories = new ObservableCollection<NavCategoryViewModel>(categories.Where(c => !c.IsPinnedToBottom));
        PinnedCategories = new ObservableCollection<NavCategoryViewModel>(categories.Where(c => c.IsPinnedToBottom));
        _navigator.NavigationRequested += OnNavigationRequested;
    }

    /// <summary>Selects the page a feature asked for (see <see cref="IPageNavigator"/>): its category
    /// and its tab. The nav rail's OneWay SelectedItem bindings follow <see cref="SelectedCategory"/>.</summary>
    private void OnNavigationRequested(Type pageType)
    {
        var page = _allCategories.SelectMany(c => c.Pages).FirstOrDefault(p => p.GetType() == pageType);
        if (page is null)
        {
            _logger.LogWarning("A feature asked to show {PageType}, which is not a registered page.", pageType.Name);
            return;
        }

        SelectedPage = page;
    }

    /// <summary>Unpinned categories shown in the nav rail, in order.</summary>
    public ObservableCollection<NavCategoryViewModel> Categories { get; }

    /// <summary>Categories shown in their own group at the bottom of the nav rail (Get help, Settings).</summary>
    public ObservableCollection<NavCategoryViewModel> PinnedCategories { get; }

    public bool IsElevated => _shellService.IsElevated;

    public string AdminStatusText => IsElevated ? "Running as administrator" : "Not running as administrator";

    /// <summary>Shown as small text in the sidebar footer, e.g. "Version 0.1.0". Read from the
    /// entry assembly's informational version (set from the single `Version` in
    /// Directory.Build.props) rather than duplicated here. Computed once into a field (rather than
    /// an expression-bodied property) since the value never changes for the process lifetime.</summary>
    public string VersionText { get; } = $"Version {GetAppVersion()}";

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var informational = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return assembly?.GetName().Version?.ToString() ?? "0.0.0";
        }

        // Strip any source-control metadata suffix (e.g. "0.1.0+abcdef1234").
        var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
        return plusIndex < 0 ? informational : informational[..plusIndex];
    }

    public IRelayCommand RestartElevatedCommand => _shellService.RestartElevatedCommand;

    /// <summary>The first page reporting work in flight (see <see cref="IBusyGuard"/>), or null.</summary>
    public IBusyGuard? FindBusyPage()
    {
        foreach (var page in _allCategories.SelectMany(c => c.Pages))
        {
            if (page is IBusyGuard { IsBusyWithWork: true } guard)
            {
                return guard;
            }
        }

        return null;
    }

    /// <summary>
    /// Selects and awaits the load of the first page. Called from <see cref="MainWindow"/>'s
    /// Loaded event rather than the constructor, so DI resolution never triggers page work.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Idempotent: a tray start (--tray) can navigate (e.g. tray "Get help") before the window's
        // first Loaded runs this, and that choice must not be reset to the first category.
        SelectedCategory ??= Categories.FirstOrDefault();
        await _currentNavigation.ConfigureAwait(true);
    }

    /// <summary>Selecting a category (rail click) shows the tab last used in it.</summary>
    partial void OnSelectedCategoryChanged(NavCategoryViewModel? value)
    {
        if (value is not null)
        {
            SelectedPage = value.SelectedPage;
        }
    }

    /// <summary>
    /// Every navigation - the first one included - cancels whichever page load was in flight,
    /// swaps in the newly resolved view, and tells the newly selected page to (re)load.
    /// </summary>
    partial void OnSelectedPageChanged(IPage? value)
    {
        // Keep category and remembered tab in step with the page, whichever way it was selected.
        var category = value is null ? null : _allCategories.FirstOrDefault(c => c.Pages.Contains(value));
        if (category is not null)
        {
            category.SelectedPage = value!;
            SelectedCategory = category;
        }

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

    /// <summary>Idempotent, so a second call never cancels an already-disposed
    /// <see cref="CancellationTokenSource"/> (which throws).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _navigator.NavigationRequested -= OnNavigationRequested;
        foreach (var category in _allCategories)
        {
            category.Dispose();
        }

        _navigationCts.Cancel();
        _navigationCts.Dispose();
    }
}
