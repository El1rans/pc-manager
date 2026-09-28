using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Setup;
using Porchlight.App.Shell;
using Xunit;

namespace Porchlight.App.Tests.Shell;

/// <summary>
/// Regression coverage for the shell's nav rail selection logic - see
/// docs/specs/08-rebrand-porchlight.md's PR review: the nav rail's two ListBoxes (the main list and
/// the pinned-at-bottom "Get help" group, both shown in <c>MainWindow.xaml</c>) used to leave a
/// stale item highlighted in one list after picking an item in the other, because both were bound
/// to this same <see cref="MainViewModel.SelectedPage"/> property. The actual fix lives in
/// <c>MainWindow.xaml.cs</c> (a WPF Selector/binding issue, not something a view-model-only test
/// can reach), but the invariant these tests pin down - <see cref="MainViewModel.SelectedPage"/> is
/// always exactly one page, and switching between a <see cref="MainViewModel.Pages"/> item and a
/// <see cref="MainViewModel.PinnedPages"/> item always fully replaces the previous selection rather
/// than merely adding to it - is the view-model-level contract the view's two lists both rely on.
/// </summary>
public sealed class MainViewModelTests
{
    private sealed class FakePage(string title, bool pinned, int order) : IPage
    {
        public string Title { get; } = title;
        public string Glyph => "";
        public string? Badge => null;
        public int Order { get; } = order;
        public bool IsPinnedToBottom { get; } = pinned;

        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

        public Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;
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

        // Deliberately null, not `new FrameworkElement()`: constructing any real WPF
        // FrameworkElement requires an initialized Dispatcher/Application (KeyboardNavigation's
        // static setup throws otherwise), which these plain xunit tests do not have. None of these
        // tests read SelectedPageView's content, only that MainViewModel treated a non-null
        // selection as "has a view" - see SelectingAPage_UpdatesSelectedPageView.
        public FrameworkElement GetOrCreateView(IPage page) => null!;
    }

    private sealed class FakeSetupLauncher : ISetupLauncher
    {
        public void ShowSetup()
        {
        }
    }

    private static MainViewModel CreateViewModel(out FakePage mainPage, out FakePage pinnedPage)
    {
        mainPage = new FakePage("Lighting", pinned: false, order: 1);
        pinnedPage = new FakePage("Get help", pinned: true, order: 99);

        return new MainViewModel(
            [mainPage, pinnedPage],
            new FakeShellService(),
            new FakePageViewLocator(),
            new FakeSetupLauncher(),
            NullLogger<MainViewModel>.Instance);
    }

    [Fact]
    public void SelectingAPinnedPage_ReplacesAPreviouslySelectedMainPage()
    {
        var viewModel = CreateViewModel(out var mainPage, out var pinnedPage);

        viewModel.SelectedPage = mainPage;
        viewModel.SelectedPage = pinnedPage;

        Assert.Same(pinnedPage, viewModel.SelectedPage);
        // Never simultaneously "the selection" in both groups - selecting the pinned page must
        // fully replace the main page's selection, not merely add to it.
        Assert.DoesNotContain(viewModel.Pages, p => ReferenceEquals(p, viewModel.SelectedPage));
        Assert.Contains(viewModel.PinnedPages, p => ReferenceEquals(p, viewModel.SelectedPage));
    }

    [Fact]
    public void SelectingAMainPage_ReplacesAPreviouslySelectedPinnedPage()
    {
        var viewModel = CreateViewModel(out var mainPage, out var pinnedPage);

        viewModel.SelectedPage = pinnedPage;
        viewModel.SelectedPage = mainPage;

        Assert.Same(mainPage, viewModel.SelectedPage);
        Assert.DoesNotContain(viewModel.PinnedPages, p => ReferenceEquals(p, viewModel.SelectedPage));
        Assert.Contains(viewModel.Pages, p => ReferenceEquals(p, viewModel.SelectedPage));
    }

    [Fact]
    public async Task InitializeAsync_SelectsTheFirstMainPage_NeverAPinnedOne()
    {
        var viewModel = CreateViewModel(out var mainPage, out var pinnedPage);

        await viewModel.InitializeAsync(CancellationToken.None);

        Assert.Same(mainPage, viewModel.SelectedPage);
        Assert.NotSame(pinnedPage, viewModel.SelectedPage);
    }
}
