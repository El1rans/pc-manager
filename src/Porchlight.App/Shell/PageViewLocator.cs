using System.Runtime.CompilerServices;
using System.Windows;

namespace Porchlight.App.Shell;

/// <inheritdoc cref="IPageViewLocator"/>
public sealed class PageViewLocator : IPageViewLocator
{
    private readonly Dictionary<Type, Func<FrameworkElement>> _factories = new();
    private readonly ConditionalWeakTable<IPage, FrameworkElement> _cache = new();

    public void RegisterFactory(Type viewModelType, Func<FrameworkElement> createView) =>
        _factories[viewModelType] = createView;

    public FrameworkElement GetOrCreateView(IPage page)
    {
        if (_cache.TryGetValue(page, out var cached))
        {
            return cached;
        }

        if (!_factories.TryGetValue(page.GetType(), out var factory))
        {
            throw new InvalidOperationException(
                $"No view registered for page type '{page.GetType()}'. Call services.AddPage<TViewModel, TView>() for it.");
        }

        var view = factory();
        view.DataContext = page;
        _cache.Add(page, view);
        return view;
    }
}
