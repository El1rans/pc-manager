using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Winget;

/// <summary>
/// Parses the fixed-width table(s) printed by <c>winget upgrade</c>. Ported from
/// <c>ConvertFrom-WingetTable</c> in <c>prototype/WingetUpdater.ps1</c>, which proved this approach
/// against real winget output.
/// </summary>
/// <remarks>
/// winget right-pads each column to line up under its header, then draws a dashed separator line
/// under the header row. Column start positions come from where each header word begins (found via
/// regex, not by splitting on whitespace, since a name column's contents may itself contain spaces);
/// each data row is then sliced at those same offsets rather than split on whitespace, so a "Name"
/// containing spaces (e.g. "Microsoft Visual Studio Code (User)") stays intact. A table ends at a
/// blank line or a line too short to reach the 4th column (winget's own summary line, e.g. "10
/// upgrades available."). winget sometimes prints a second table, headed by a sentence explaining
/// that those packages need an exact <c>--id</c> match to upgrade - rows from any table after the
/// first have <see cref="WingetPackage.RequiresExplicit"/> set.
/// </remarks>
/// <remarks>
/// winget pads columns to a fixed <i>display-cell</i> width, not a fixed character count: a wide
/// (East Asian) character such as a CJK ideogram occupies two display cells but is still one
/// <see cref="char"/> (or one <see cref="System.Text.Rune"/>). The header line is always plain
/// ASCII, so its column start offsets are the same whether measured in characters or display
/// cells - but slicing a <i>data</i> row at those same offsets as raw string indices would cut a
/// row with wide characters in the wrong place, since each wide character in an earlier column
/// consumes two cells but only one string index. Every offset is therefore treated as a display-cell
/// position and mapped back to that specific row's own string index via <see cref="MapCellOffsetsToStringIndices"/>
/// before slicing.
/// </remarks>
public static partial class WingetTableParser
{
    public static IReadOnlyList<WingetPackage> Parse(IReadOnlyList<string> lines, ILogger? logger = null)
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
                if (string.IsNullOrWhiteSpace(line) || DisplayWidth(line) <= columnStarts[3])
                {
                    break;
                }

                var fields = SliceFields(line, columnStarts);

                // The Id column must be a single, contiguous token; anything else (e.g. a
                // continuation line, or a summary sentence that slipped past the length check) is
                // not a real data row.
                if (!IdFieldRegex().IsMatch(fields[1]))
                {
                    if (logger is not null)
                    {
                        LogDroppedRow(logger, line);
                    }

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

    /// <summary>Column start offsets, in display cells: the position of the first character of each
    /// whitespace-separated word on the header line (which - being plain ASCII - has one display
    /// cell per character, so its char index doubles as a cell offset).</summary>
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
    /// start offsets (display cells) - a column's text runs from its own start up to (but not
    /// including) the next column's start, or to the end of the line for the last column.</summary>
    private static List<string> SliceFields(string line, List<int> columnStarts)
    {
        var stringIndices = MapCellOffsetsToStringIndices(line, columnStarts);

        List<string> fields = new(columnStarts.Count);
        for (var c = 0; c < columnStarts.Count; c++)
        {
            var start = stringIndices[c];
            if (start >= line.Length)
            {
                fields.Add(string.Empty);
                continue;
            }

            var end = c + 1 < columnStarts.Count ? Math.Min(stringIndices[c + 1], line.Length) : line.Length;
            fields.Add(line[start..end].Trim());
        }

        return fields;
    }

    /// <summary>Converts each display-cell column offset into the string index within
    /// <paramref name="line"/> where that display-cell position is first reached - the inverse of
    /// how winget originally padded the column to that cell width. An offset past the end of the
    /// line's own display width maps to <c>line.Length</c> (an absent/empty trailing column).</summary>
    private static int[] MapCellOffsetsToStringIndices(string line, List<int> cellOffsets)
    {
        var indices = new int[cellOffsets.Count];
        var cellPosition = 0;
        var stringIndex = 0;
        var nextTarget = 0;

        foreach (var rune in line.EnumerateRunes())
        {
            while (nextTarget < cellOffsets.Count && cellOffsets[nextTarget] <= cellPosition)
            {
                indices[nextTarget] = stringIndex;
                nextTarget++;
            }

            if (nextTarget >= cellOffsets.Count)
            {
                return indices;
            }

            cellPosition += CellWidth(rune);
            stringIndex += rune.Utf16SequenceLength;
        }

        while (nextTarget < cellOffsets.Count)
        {
            indices[nextTarget] = stringIndex;
            nextTarget++;
        }

        return indices;
    }

    /// <summary>Total display-cell width of a line - see the type remarks on wide characters.</summary>
    private static int DisplayWidth(string line)
    {
        var width = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            width += CellWidth(rune);
        }

        return width;
    }

    /// <summary>
    /// How many terminal display cells one character occupies: 0 for a non-spacing combining mark,
    /// 2 for a "wide" or "fullwidth" East Asian character (approximated by the common Unicode
    /// ranges below - CJK ideographs, Hiragana/Katakana, Hangul, fullwidth forms, etc. - rather than
    /// the full East Asian Width property table), 1 otherwise.
    /// </summary>
    private static int CellWidth(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        return IsWide(rune.Value) ? 2 : 1;
    }

    private static bool IsWide(int codePoint) =>
        codePoint is >= 0x1100 and <= 0x115F // Hangul Jamo
        or 0x2329 or 0x232A
        || codePoint is >= 0x2E80 and <= 0x303E // CJK radicals, Kangxi, CJK symbols/punctuation
        or >= 0x3041 and <= 0x33FF // Hiragana .. CJK Compatibility
        or >= 0x3400 and <= 0x4DBF // CJK Unified Ideographs Extension A
        or >= 0x4E00 and <= 0x9FFF // CJK Unified Ideographs
        or >= 0xA000 and <= 0xA4CF // Yi
        or >= 0xAC00 and <= 0xD7A3 // Hangul syllables
        or >= 0xF900 and <= 0xFAFF // CJK compatibility ideographs
        or >= 0xFE30 and <= 0xFE4F // CJK compatibility forms
        or >= 0xFF00 and <= 0xFF60 // Fullwidth forms
        or >= 0xFFE0 and <= 0xFFE6
        or >= 0x20000 and <= 0x3FFFD; // CJK extension B and beyond, supplementary ideographic plane

    [GeneratedRegex(@"^\s*-{10,}\s*$")]
    private static partial Regex SeparatorLineRegex();

    [GeneratedRegex(@"\S+")]
    private static partial Regex NonWhitespaceRunRegex();

    [GeneratedRegex(@"^\S+$")]
    private static partial Regex IdFieldRegex();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped unparsable winget upgrade row: {Line}")]
    private static partial void LogDroppedRow(ILogger logger, string line);
}
