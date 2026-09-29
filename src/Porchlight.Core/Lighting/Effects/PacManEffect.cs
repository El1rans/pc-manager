namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// Pac-Man eats his way across the matrix in a boustrophedon path (row 0 left-to-right, row 1
/// right-to-left, and so on), leaving eaten cells dark and unvisited cells lit as "dots", chasing a
/// ghost fleeing a few cells ahead of him (it is cornered on the last cell, where he catches it)
/// and the path refilling once he reaches the end - see
/// docs/specs/11-led-effects.md. Matrix-only: on a non-matrix layout this renders nothing (the
/// buffer is left at its default, black).
/// </summary>
public sealed class PacManEffect : IEffect
{
    private static readonly RgbColor PacManColor = new(0xFF, 0xE0, 0x00);
    private static readonly RgbColor DotColor = new(0x60, 0x60, 0xFF);
    private static readonly RgbColor GhostColor = new(0xFF, 0x30, 0x30);
    private static readonly RgbColor EatenColor = RgbColor.Black;

    /// <summary>Path for the layout last rendered - a layout is immutable and built once per device,
    /// so this is recomputed only if the layout reference changes. One immutable pair in a single
    /// field, so a render on another thread can never see a mismatched layout/path.</summary>
    private (LedLayout Layout, IReadOnlyList<int> Path)? _cachedPath;

    /// <param name="cellsPerSecond">How many grid cells Pac-Man advances per second. Must be
    /// positive.</param>
    /// <param name="ghostLeadCells">How many cells ahead of Pac-Man the ghost flees.</param>
    public PacManEffect(double cellsPerSecond = 6.0, int ghostLeadCells = 2)
    {
        if (cellsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellsPerSecond), cellsPerSecond, "Must be positive.");
        }

        if (ghostLeadCells < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ghostLeadCells), ghostLeadCells, "Cannot be negative.");
        }

        CellsPerSecond = cellsPerSecond;
        GhostLeadCells = ghostLeadCells;
    }

    public string Name => "Pac-Man";

    public double CellsPerSecond { get; }

    public int GhostLeadCells { get; }

    /// <summary>Builds the boustrophedon traversal path as indices into <paramref name="layout"/>'s
    /// points: row 0 left-to-right, row 1 right-to-left, and so on, skipping any grid cell with no
    /// LED (a matrix hole). Public/static and pure so tests can check the path shape directly.</summary>
    public static IReadOnlyList<int> BuildPath(LedLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!layout.IsMatrix)
        {
            return [];
        }

        var byCell = new Dictionary<(int Col, int Row), int>();
        for (var i = 0; i < layout.Points.Count; i++)
        {
            var point = layout.Points[i];
            if (point.Col is { } col && point.Row is { } row)
            {
                byCell[(col, row)] = i;
            }
        }

        var columnCount = layout.ColumnCount!.Value;
        var rowCount = layout.RowCount!.Value;
        var path = new List<int>(layout.Count);

        for (var row = 0; row < rowCount; row++)
        {
            var leftToRight = row % 2 == 0;
            for (var step = 0; step < columnCount; step++)
            {
                var col = leftToRight ? step : columnCount - 1 - step;
                if (byCell.TryGetValue((col, row), out var pointIndex))
                {
                    path.Add(pointIndex);
                }
            }
        }

        return path;
    }

    public void Render(in EffectFrame frame, Span<RgbColor> buffer)
    {
        var layout = frame.Layout;
        if (!layout.IsMatrix)
        {
            return;
        }

        var cached = _cachedPath;
        if (cached is null || !ReferenceEquals(cached.Value.Layout, layout))
        {
            cached = (layout, BuildPath(layout));
            _cachedPath = cached;
        }

        var path = cached.Value.Path;
        if (path.Count == 0)
        {
            return;
        }

        buffer.Fill(DotColor);

        var totalSteps = (int)Math.Floor(frame.Elapsed.TotalSeconds * CellsPerSecond);
        var currentStep = ((totalSteps % path.Count) + path.Count) % path.Count;

        for (var step = 0; step < currentStep; step++)
        {
            buffer[path[step]] = EatenColor;
        }

        // The ghost flees ahead along the same path; it cannot run past the end, so it is cornered
        // on the last cell and Pac-Man catches it there just before the path refills.
        var ghostStep = Math.Min(currentStep + GhostLeadCells, path.Count - 1);
        buffer[path[ghostStep]] = GhostColor;

        buffer[path[currentStep]] = PacManColor;
    }
}
