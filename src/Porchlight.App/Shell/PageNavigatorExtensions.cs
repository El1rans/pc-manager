namespace Porchlight.App.Shell;

/// <summary>Type-safe wrapper over <see cref="IPageNavigator.NavigateTo(Type)"/>.</summary>
public static class PageNavigatorExtensions
{
    /// <summary>Asks the shell to show the page whose view model is <typeparamref name="TPage"/>.</summary>
    public static void NavigateTo<TPage>(this IPageNavigator navigator)
        where TPage : IPage => navigator.NavigateTo(typeof(TPage));
}
