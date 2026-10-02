using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Winget;

/// <summary>
/// Parses the table printed by <c>winget search</c> (columns Name, Id, Version and, for a
/// <c>--query</c> search, Match). Column splitting is shared with <see cref="WingetTableParser"/>.
/// "No package found matching input criteria." has no table and so yields an empty list.
/// </summary>
public static class WingetSearchTableParser
{
    private const int MinimumColumns = 3;

    /// <summary>winget's marker for a cell it had to cut short to fit the console width.</summary>
    private const char TruncationMarker = '…';

    public static IReadOnlyList<WingetSearchResult> Parse(IReadOnlyList<string> lines, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var results = new List<WingetSearchResult>();
        foreach (var row in WingetTableParser.ParseRows(lines, MinimumColumns, logger))
        {
            var id = row.Fields[1];

            // A truncated id is not a real package id and must never reach "winget install --id".
            if (id.Contains(TruncationMarker, StringComparison.Ordinal))
            {
                continue;
            }

            results.Add(new WingetSearchResult(row.Fields[0], id, row.Fields[2]));
        }

        return results;
    }
}
