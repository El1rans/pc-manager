using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.App.Controls;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Dashboard;

/// <summary>
/// One metric tile (CPU, Memory, GPU, Disk, Download, Upload): a title, a big current value, a
/// detail line, and a 60-second <c>Sparkline</c>. Percent tiles use a fixed 0-100
/// <see cref="Maximum"/>; throughput tiles pass <see cref="double.NaN"/> so the chart auto-scales.
/// </summary>
public sealed partial class MetricTileViewModel : ObservableObject
{
    /// <summary>How many consecutive null samples before the tile gives up and shows "Not available
    /// on this PC", rather than reacting to a single transient miss.</summary>
    private const int ConsecutiveMissesBeforeUnavailable = 5;

    private readonly RollingSeries _series = new();
    private readonly Func<double, string> _formatForTooltip;
    private int _consecutiveMisses;

    public MetricTileViewModel(string title, double maximum, Func<double, string> formatForTooltip, Hue hue = Hue.Neutral, string glyph = "")
    {
        Title = title;
        Maximum = maximum;
        _formatForTooltip = formatForTooltip;
        Hue = hue;
        Glyph = glyph;
    }

    public string Title { get; }

    /// <summary>The tile's colour: icon chip, value, sparkline and card wash (docs/specs/40-vivid-colour.md).</summary>
    public Hue Hue { get; }

    /// <summary>Segoe Fluent Icons glyph shown in the tile's icon chip.</summary>
    public string Glyph { get; }

    /// <summary>Top of the Sparkline's scale; <see cref="double.NaN"/> auto-scales to the data.</summary>
    public double Maximum { get; }

    /// <summary>Neutral until the first real sample arrives - never "n/a" or "Not available" before
    /// the sampler has had a chance to report anything.</summary>
    [ObservableProperty]
    private string _valueText = "–";

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private IReadOnlyList<double> _values = [];

    [ObservableProperty]
    private string _tooltipText = "Last 60s - no data yet";

    /// <summary>
    /// Applies one tick's sample. A null <paramref name="sample"/> means this tick had no value
    /// (the underlying source failed to read, or is still warming up): a single miss keeps showing
    /// the last good value rather than flashing to "unavailable", since most misses are transient.
    /// Only after <see cref="ConsecutiveMissesBeforeUnavailable"/> misses in a row does the tile
    /// switch to its "Not available on this PC" state, on the assumption the source is permanently
    /// gone (e.g. the counter category does not exist on this PC).
    /// </summary>
    public void Update(double? sample, string valueText, string detailText)
    {
        if (sample is null)
        {
            _consecutiveMisses++;
            if (_consecutiveMisses >= ConsecutiveMissesBeforeUnavailable)
            {
                IsAvailable = false;
                ValueText = "n/a";
                DetailText = "Not available on this PC";
            }

            return;
        }

        _consecutiveMisses = 0;
        IsAvailable = true;
        ValueText = valueText;
        DetailText = detailText;
        _series.Add(sample.Value);
        Values = _series.Snapshot();
        TooltipText = $"Last 60s - min {_formatForTooltip(_series.Min())}, " +
                      $"avg {_formatForTooltip(_series.Average())}, " +
                      $"max {_formatForTooltip(_series.Max())}";
    }
}
