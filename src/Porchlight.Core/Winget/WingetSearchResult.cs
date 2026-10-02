namespace Porchlight.Core.Winget;

/// <summary>One row of a <c>winget search</c> table, as parsed by <see cref="WingetSearchTableParser"/>.</summary>
/// <param name="Name">Display name, e.g. <c>VLC media player</c>.</param>
/// <param name="Id">Exact winget package identifier, e.g. <c>VideoLAN.VLC</c>.</param>
/// <param name="Version">Latest version available from the source (empty for the curated popular list).</param>
public sealed record WingetSearchResult(string Name, string Id, string Version);
