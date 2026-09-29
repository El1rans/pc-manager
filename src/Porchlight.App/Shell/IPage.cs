using System.ComponentModel;

namespace Porchlight.App.Shell;

/// <summary>
/// A page shown in the navigation rail. Implemented by each feature's top-level view model.
/// </summary>
public interface IPage : INotifyPropertyChanged
{
    /// <summary>Page name; also the tab text unless <see cref="TabTitle"/> differs.</summary>
    string Title { get; }

    /// <summary>Segoe Fluent Icons glyph shown next to the title.</summary>
    string Glyph { get; }

    /// <summary>Optional badge text (e.g. a count). Observable; null/empty hides the badge.</summary>
    string? Badge { get; }

    /// <summary>Tab text when the page is shown as a tab in its category. Defaults to
    /// <see cref="Title"/>.</summary>
    string TabTitle { get; }

    /// <summary>The navigation category (rail entry) this page belongs to.</summary>
    PageCategory Category { get; }

    /// <summary>Position of the page's tab within its <see cref="Category"/>, ascending.</summary>
    int Order { get; }

    /// <summary>Called on first navigation to the page and every subsequent navigation to it.</summary>
    Task OnNavigatedToAsync(CancellationToken cancellationToken);
}
