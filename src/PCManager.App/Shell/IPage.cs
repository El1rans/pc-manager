namespace PCManager.App.Shell;

/// <summary>
/// A page shown in the navigation rail. Implemented by each feature's top-level view model.
/// </summary>
public interface IPage
{
    /// <summary>Display name shown in the navigation rail.</summary>
    string Title { get; }

    /// <summary>Segoe Fluent Icons glyph shown next to the title.</summary>
    string Glyph { get; }

    /// <summary>Optional badge text (e.g. a count). Observable; null/empty hides the badge.</summary>
    string? Badge { get; }

    /// <summary>Position in the navigation rail, ascending.</summary>
    int Order { get; }

    /// <summary>Called on first navigation to the page and every subsequent navigation to it.</summary>
    Task OnNavigatedToAsync(CancellationToken cancellationToken);
}
