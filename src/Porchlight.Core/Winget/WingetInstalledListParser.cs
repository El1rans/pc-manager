using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Winget;

/// <summary>Reads the Name, Id and Version columns of a <c>winget list</c> table. Column splitting is
/// shared with <see cref="WingetTableParser"/>.</summary>
public static class WingetInstalledListParser
{
    private const int MinimumColumns = 3;

    public static IReadOnlyList<WingetInstalledPackage> Parse(IReadOnlyList<string> lines, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return WingetTableParser.ParseRows(lines, MinimumColumns, logger)
            .Select(row => new WingetInstalledPackage(row.Fields[0], row.Fields[1], row.Fields[2]))
            .ToList();
    }
}
