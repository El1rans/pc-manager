namespace Porchlight.App.Shell;

/// <summary>
/// Lets one feature send the user to another page (for example the Dashboard's "Free up space"
/// button) without depending on <see cref="MainViewModel"/>. Call from the UI thread.
/// </summary>
public interface IPageNavigator
{
    /// <summary>Raised when a page is requested; <see cref="MainViewModel"/> handles it by selecting
    /// the page.</summary>
    event Action<Type>? NavigationRequested;

    /// <summary>Asks the shell to show the page whose view model type is
    /// <paramref name="pageViewModelType"/> (its category and tab). Unknown types are ignored.</summary>
    void NavigateTo(Type pageViewModelType);
}
