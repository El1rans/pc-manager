namespace Porchlight.App.Shell;

/// <inheritdoc cref="IPageNavigator"/>
public sealed class PageNavigator : IPageNavigator
{
    public event Action<Type>? NavigationRequested;

    public void NavigateTo<TPage>()
        where TPage : IPage => NavigationRequested?.Invoke(typeof(TPage));
}
