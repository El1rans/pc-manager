using System.ComponentModel;

namespace PCManager.App.Shell;

/// <summary>
/// A page shown in the navigation rail. Implemented by each feature's top-level view model.
/// </summary>
public interface IPage : INotifyPropertyChanged
{
    /// <summary>Display name shown in the navigation rail.</summary>
    string Title { get; }

    /// <summary>Segoe Fluent Icons glyph shown next to the title.</summary>
    string Glyph { get; }

    /// <summary>Optional badge text (e.g. a count). Observable; null/empty hides the badge.</summary>
    string? Badge { get; }

    /// <summary>Position in the navigation rail, ascending. Ignored for a page with
    /// <see cref="IsPinnedToBottom"/> set, which is placed by that instead.</summary>
    int Order { get; }

    /// <summary>
    /// When true, the shell places this page in its own group at the very bottom of the nav rail
    /// (above the admin block), separated and styled distinctly, instead of among the regular pages
    /// ordered by <see cref="Order"/>. For a page that is deliberately the least prominent one (e.g.
    /// "Get help") rather than a peer of the main features - see docs/specs/06-remote-support.md.
    /// </summary>
    bool IsPinnedToBottom { get; }

    /// <summary>Called on first navigation to the page and every subsequent navigation to it.</summary>
    Task OnNavigatedToAsync(CancellationToken cancellationToken);
}
