using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Setup;
using Porchlight.App.Shell;
using Xunit;

namespace Porchlight.App.Tests.Shell;

/// <summary>
/// View-model coverage for the shell's category rail and tab selection (docs/specs/22-nav-categories.md).
/// The nav rail's ListBoxes are bound one-way and driven from <c>MainWindow.xaml.cs</c> (a WPF
/// Selector/binding issue a view-model-only test cannot reach), but the invariants pinned down here -
/// <see cref="MainViewModel.SelectedPage"/> is always exactly one page and always agrees with
/// <see cref="MainViewModel.SelectedCategory"/> - are the contract the view relies on.
/// </summary>
public sealed class MainViewModelTests
{
    private class FakePage(string title, PageCategory category, int order, string? badge = null) : IPage
    {
        private string? _badge = badge;

        public string Title { get; } = title;
        public string TabTitle => Title;
        public string Glyph => "";
        public PageCategory Category { get; } = category;
        public int Order { get; } = order;
        public int NavigatedCount { get; private set; }

        public string? Badge
        {
            get => _badge;
            set
            {
                _badge = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Badge)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            NavigatedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class DashPage() : FakePage("Dashboard", PageCategory.Overview, 0);

    private sealed class UpdatesPage() : FakePage("Updates", PageCategory.TuneUp, 1);

    private sealed class StartupPage() : FakePage("Startup apps", PageCategory.TuneUp, 2);

    private sealed class HelpPage() : FakePage("Get help", PageCategory.Help, 1);

    private sealed class UnregisteredPage() : FakePage("x", PageCategory.Overview, 0);

    private sealed class BusyPage(PageCategory category) : FakePage("Busy", category, 0), IBusyGuard
    {
        public bool IsBusyWithWork => true;

        public string BusyMessage => "busy";
    }

    private sealed class FakeShellService : IShellService
    {
        public bool IsElevated => false;

        public IRelayCommand RestartElevatedCommand { get; } = new RelayCommand(() => { });
    }

    private sealed class FakePageViewLocator : IPageViewLocator
    {
        public void RegisterFactory(Type viewModelType, Func<FrameworkElement> createView)
        {
            // Not exercised by these tests - MainViewModel only calls GetOrCreateView.
        }

        // Deliberately null: constructing a real WPF FrameworkElement needs an initialized
        // Dispatcher/Application, which these plain xunit tests do not have.
        public FrameworkElement GetOrCreateView(IPage page) => null!;
    }

    private sealed class FakeSetupLauncher : ISetupLauncher
    {
        public void ShowSetup()
        {
        }
    }

    private sealed record Setup(MainViewModel Vm, DashPage Dash, UpdatesPage Updates, StartupPage Startup, HelpPage Help);

    private static MainViewModel Create(IEnumerable<IPage> pages, IPageNavigator? navigator = null) =>
        new(
            pages,
            new FakeShellService(),
            new FakePageViewLocator(),
            new FakeSetupLauncher(),
            navigator ?? new PageNavigator(),
            NullLogger<MainViewModel>.Instance);

    private static Setup CreateStandard(IPageNavigator? navigator = null)
    {
        DashPage dash = new();
        UpdatesPage updates = new();
        StartupPage startup = new();
        HelpPage help = new();

        // Deliberately registered out of order.
        return new Setup(Create([help, startup, dash, updates], navigator), dash, updates, startup, help);
    }

    [Fact]
    public void Categories_AreGroupedAndOrdered_WithHelpPinnedSeparately()
    {
        var s = CreateStandard();

        Assert.Equal(["Overview", "Tune-up"], s.Vm.Categories.Select(c => c.Title));
        Assert.Equal(["Get help"], s.Vm.PinnedCategories.Select(c => c.Title));
        Assert.Equal<IPage>([s.Updates, s.Startup], s.Vm.Categories[1].Pages);
        Assert.Same(s.Help, s.Vm.PinnedCategories[0].Pages[0]);
    }

    [Fact]
    public void HasTabs_OnlyWhenTheCategoryHasMoreThanOnePage()
    {
        var vm = CreateStandard().Vm;

        Assert.False(vm.Categories[0].HasTabs);
        Assert.True(vm.Categories[1].HasTabs);
        Assert.False(vm.PinnedCategories[0].HasTabs);
    }

    [Fact]
    public void CategoryCatalog_HasTheFiveSpecCategories_OnlyHelpPinned()
    {
        var all = PageCategoryCatalog.All.ToList();

        Assert.Equal(5, all.Count);
        Assert.Equal([PageCategory.Help], all.Where(c => c.IsPinnedToBottom).Select(c => c.Category));
        Assert.Equal(
            [PageCategory.Overview, PageCategory.TuneUp, PageCategory.InternetAndSafety, PageCategory.Hardware],
            all.Where(c => !c.IsPinnedToBottom).OrderBy(c => c.Order).Select(c => c.Category));
    }

    [Fact]
    public async Task InitializeAsync_SelectsTheFirstCategoryAndItsFirstPage()
    {
        var s = CreateStandard();

        await s.Vm.InitializeAsync(CancellationToken.None);

        Assert.Same(s.Vm.Categories[0], s.Vm.SelectedCategory);
        Assert.Same(s.Dash, s.Vm.SelectedPage);
        Assert.Equal(1, s.Dash.NavigatedCount);
    }

    [Fact]
    public async Task InitializeAsync_KeepsAnEarlierSelection_SoATrayStartCanNavigateFirst()
    {
        var s = CreateStandard();
        s.Vm.SelectedCategory = s.Vm.Categories[1];

        await s.Vm.InitializeAsync(CancellationToken.None);

        Assert.Same(s.Vm.Categories[1], s.Vm.SelectedCategory);
        Assert.Same(s.Updates, s.Vm.SelectedPage);
    }

    [Fact]
    public void SelectingACategory_SelectsItsFirstPage_AndLoadsIt()
    {
        var s = CreateStandard();

        s.Vm.SelectedCategory = s.Vm.Categories[1];

        Assert.Same(s.Updates, s.Vm.SelectedPage);
        Assert.Equal(1, s.Updates.NavigatedCount);
    }

    [Fact]
    public void SelectingATab_UpdatesTheCategoryAndTheSelectedPage()
    {
        var s = CreateStandard();
        s.Vm.SelectedCategory = s.Vm.Categories[1];

        s.Vm.SelectedPage = s.Startup;

        Assert.Same(s.Startup, s.Vm.SelectedPage);
        Assert.Same(s.Startup, s.Vm.Categories[1].SelectedPage);
        Assert.Same(s.Vm.Categories[1], s.Vm.SelectedCategory);
        Assert.Equal(1, s.Startup.NavigatedCount);
    }

    [Fact]
    public void ReselectingACategory_ReturnsToTheTabLastUsedInIt()
    {
        var s = CreateStandard();
        s.Vm.SelectedCategory = s.Vm.Categories[1];
        s.Vm.SelectedPage = s.Startup;
        s.Vm.SelectedCategory = s.Vm.Categories[0];

        s.Vm.SelectedCategory = s.Vm.Categories[1];

        Assert.Same(s.Startup, s.Vm.SelectedPage);
    }

    [Fact]
    public void SelectingAPinnedCategory_ReplacesAMainSelection_AndBack()
    {
        var s = CreateStandard();
        s.Vm.SelectedCategory = s.Vm.Categories[0];

        s.Vm.SelectedCategory = s.Vm.PinnedCategories[0];
        Assert.Same(s.Help, s.Vm.SelectedPage);
        Assert.Same(s.Vm.PinnedCategories[0], s.Vm.SelectedCategory);

        s.Vm.SelectedCategory = s.Vm.Categories[0];
        Assert.Same(s.Dash, s.Vm.SelectedPage);
    }

    [Fact]
    public void NavigateTo_SelectsTheCategoryAndTab_OfThatViewModelType()
    {
        var navigator = new PageNavigator();
        var s = CreateStandard(navigator);
        s.Vm.SelectedPage = s.Dash;

        navigator.NavigateTo<StartupPage>();

        Assert.Same(s.Startup, s.Vm.SelectedPage);
        Assert.Same(s.Vm.Categories[1], s.Vm.SelectedCategory);
        Assert.Same(s.Startup, s.Vm.SelectedCategory!.SelectedPage);
    }

    [Fact]
    public void NavigateTo_APinnedPage_SelectsThePinnedCategory()
    {
        var navigator = new PageNavigator();
        var s = CreateStandard(navigator);
        s.Vm.SelectedPage = s.Dash;

        navigator.NavigateTo(typeof(HelpPage));

        Assert.Same(s.Help, s.Vm.SelectedPage);
        Assert.Same(s.Vm.PinnedCategories[0], s.Vm.SelectedCategory);
    }

    [Fact]
    public void NavigateTo_AnUnregisteredPage_KeepsTheCurrentSelection()
    {
        var navigator = new PageNavigator();
        var s = CreateStandard(navigator);
        s.Vm.SelectedPage = s.Dash;

        navigator.NavigateTo<UnregisteredPage>();

        Assert.Same(s.Dash, s.Vm.SelectedPage);
    }

    [Fact]
    public void Dispose_StopsListeningForNavigationRequests()
    {
        var navigator = new PageNavigator();
        var s = CreateStandard(navigator);
        s.Vm.SelectedPage = s.Dash;
        s.Vm.Dispose();

        navigator.NavigateTo<StartupPage>();

        Assert.Same(s.Dash, s.Vm.SelectedPage);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var vm = CreateStandard().Vm;

        vm.Dispose();
        var exception = Record.Exception(vm.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void FindBusyPage_ChecksEveryPage_PinnedOnesIncluded()
    {
        BusyPage busy = new(PageCategory.Help);
        var vm = Create([new DashPage(), busy]);

        Assert.Same(busy, vm.FindBusyPage());
    }

    [Fact]
    public void FindBusyPage_IsNullWhenNothingIsBusy() => Assert.Null(CreateStandard().Vm.FindBusyPage());

    private static NavCategoryViewModel TuneUp(params IPage[] pages) =>
        new(PageCategoryCatalog.Get(PageCategory.TuneUp), pages);

    [Fact]
    public void Badge_SumsNumericPageBadges()
    {
        using var category = TuneUp(
            new FakePage("A", PageCategory.TuneUp, 1, "3"),
            new FakePage("B", PageCategory.TuneUp, 2, "4"),
            new FakePage("C", PageCategory.TuneUp, 3));

        Assert.Equal("7", category.Badge);
    }

    [Fact]
    public void Badge_WithNoBadges_IsNull()
    {
        using var category = TuneUp(new FakePage("A", PageCategory.TuneUp, 1));

        Assert.Null(category.Badge);
    }

    [Fact]
    public void Badge_NonNumeric_FallsBackToTheFirstNonEmpty()
    {
        using var category = TuneUp(
            new FakePage("A", PageCategory.TuneUp, 1),
            new FakePage("B", PageCategory.TuneUp, 2, "New"),
            new FakePage("C", PageCategory.TuneUp, 3, "5"));

        Assert.Equal("New", category.Badge);
    }

    [Fact]
    public void Badge_UpdatesLive_WhenAPageBadgeChanges()
    {
        var updates = new FakePage("Updates", PageCategory.TuneUp, 1);
        using var category = TuneUp(updates);
        List<string?> raised = [];
        category.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        updates.Badge = "2";

        Assert.Equal("2", category.Badge);
        Assert.Contains(nameof(NavCategoryViewModel.Badge), raised);
    }

    [Fact]
    public void Dispose_Category_UnsubscribesFromPageChanges()
    {
        var updates = new FakePage("Updates", PageCategory.TuneUp, 1);
        var category = TuneUp(updates);

        category.Dispose();
        updates.Badge = "9";

        Assert.Null(category.Badge);
    }

    [Fact]
    public void Category_Constructor_RejectsAnEmptyPageList() =>
        Assert.Throws<ArgumentException>(() => TuneUp());
}
