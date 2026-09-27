using System.Text.RegularExpressions;

namespace PCManager.Core.Winget;

/// <summary>
/// Parses the fixed-width table(s) printed by <c>winget upgrade</c>. Ported from
/// <c>ConvertFrom-WingetTable</c> in <c>prototype/WingetUpdater.ps1</c>, which proved this approach
/// against real winget output.
/// </summary>
/// <remarks>
/// winget right-pads each column to line up under its header, then draws a dashed separator line
/// under the header row. Column start positions come from where each header word begins (found via
/// regex, not by splitting on whitespace, since a name column's contents may itself contain spaces);
/// each data row is then sliced at those same character offsets rather than split on whitespace, so
/// a "Name" containing spaces (e.g. "Microsoft Visual Studio Code (User)") stays intact. A table
/// ends at a blank line or a line too short to reach the 4th column (winget's own summary line, e.g.
/// "10 upgrades available."). winget sometimes prints a second table, headed by a sentence
/// explaining that those packages need an exact <c>--id</c> match to upgrade - rows from any table
/// after the first have <see cref="WingetPackage.RequiresExplicit"/> set.
/// </remarks>
public static partial class WingetTableParser
{
    public static IReadOnlyList<WingetPackage> Parse(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var packages = new List<WingetPackage>();
        var tableIndex = 0;

        for (var i = 1; i < lines.Count; i++)
        {
            if (!SeparatorLineRegex().IsMatch(lines[i]))
            {
                continue;
            }

            tableIndex++;
            var columnStarts = FindColumnStarts(lines[i - 1]);
            if (columnStarts.Count < 4)
            {
                continue;
            }

            var j = i + 1;
            for (; j < lines.Count; j++)
            {
                var line = lines[j];
                if (string.IsNullOrWhiteSpace(line) || line.Length <= columnStarts[3])
                {
                    break;
                }

                var fields = SliceFields(line, columnStarts);

                // The Id column must be a single, contiguous token; anything else (e.g. a
                // continuation line, or a summary sentence that slipped past the length check) is
                // not a real data row.
                if (!IdFieldRegex().IsMatch(fields[1]))
                {
                    continue;
                }

                var source = fields.Count > 4 ? fields[4] : string.Empty;
                packages.Add(new WingetPackage(
                    Name: fields[0],
                    Id: fields[1],
                    InstalledVersion: fields[2],
                    AvailableVersion: fields[3],
                    Source: source,
                    RequiresExplicit: tableIndex > 1));
            }

            i = j;
        }

        return packages;
    }

    /// <summary>Column start offsets: the index of the first character of each whitespace-separated
    /// word on the header line.</summary>
    private static List<int> FindColumnStarts(string headerLine)
    {
        List<int> starts = [];
        foreach (Match match in NonWhitespaceRunRegex().Matches(headerLine))
        {
            starts.Add(match.Index);
        }

        return starts;
    }

    /// <summary>Slices one data row into a field per column, trimmed, using the header's column
    /// start offsets - a column's text runs from its own start up to (but not including) the next
    /// column's start, or to the end of the line for the last column.</summary>
    private static List<string> SliceFields(string line, List<int> columnStarts)
    {
        List<string> fields = new(columnStarts.Count);
        for (var c = 0; c < columnStarts.Count; c++)
        {
            var start = columnStarts[c];
            if (start >= line.Length)
            {
                fields.Add(string.Empty);
                continue;
            }

            var end = c + 1 < columnStarts.Count ? Math.Min(columnStarts[c + 1], line.Length) : line.Length;
            fields.Add(line[start..end].Trim());
        }

        return fields;
    }

    [GeneratedRegex(@"^\s*-{10,}\s*$")]
    private static partial Regex SeparatorLineRegex();

    [GeneratedRegex(@"\S+")]
    private static partial Regex NonWhitespaceRunRegex();

    [GeneratedRegex(@"^\S+$")]
    private static partial Regex IdFieldRegex();
}
