using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Winget;

/// <summary>Reads the Id column of a <c>winget list</c> table (Name, Id, Version, then optionally
/// Available and Source). Column splitting is shared with <see cref="WingetTableParser"/>.</summary>
public static class WingetInstalledIdsParser
{
    private const int MinimumColumns = 3;

    /// <returns>The installed package ids, compared case-insensitively.</returns>
    public static IReadOnlySet<string> Parse(IReadOnlyList<string> lines, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in WingetTableParser.ParseRows(lines, MinimumColumns, logger))
        {
            ids.Add(row.Fields[1]);
        }

        return ids;
    }
}
