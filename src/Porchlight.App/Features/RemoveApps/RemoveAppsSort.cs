namespace Porchlight.App.Features.RemoveApps;

/// <summary>How the "Remove apps" list is ordered.</summary>
public enum RemoveAppsSort
{
    /// <summary>A to Z.</summary>
    Name,

    /// <summary>Largest first; apps with no reported size last.</summary>
    Size,

    /// <summary>Newest first; apps with no known date last.</summary>
    Date,
}

/// <summary>An entry of the "Sort by" list.</summary>
public sealed record RemoveAppsSortOption(RemoveAppsSort Sort, string Label);
