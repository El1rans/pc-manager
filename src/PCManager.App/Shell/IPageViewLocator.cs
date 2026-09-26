using System.Windows;

namespace PCManager.App.Shell;

/// <summary>
/// Resolves the view for a page's view model, so the shell's content area never needs a
/// per-feature <c>DataTemplate</c> registered in <c>App.xaml</c>.
/// </summary>
public interface IPageViewLocator
{
    /// <summary>Registers the view factory for a view model type. Called once per feature at
    /// startup, from the <see cref="PageRegistration"/>s collected in DI.</summary>
    void RegisterFactory(Type viewModelType, Func<FrameworkElement> createView);

    /// <summary>Gets (creating and caching on first use) the view for the given page.</summary>
    FrameworkElement GetOrCreateView(IPage page);
}
