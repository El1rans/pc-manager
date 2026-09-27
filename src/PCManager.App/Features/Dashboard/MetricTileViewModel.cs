using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Monitoring;

namespace PCManager.App.Features.Dashboard;

/// <summary>
/// One metric tile (CPU, Memory, GPU, Disk, Download, Upload): a title, a big current value, a
/// detail line, and a 60-second <c>Sparkline</c>. Percent tiles use a fixed 0-100
/// <see cref="Maximum"/>; throughput tiles pass <see cref="double.NaN"/> so the chart auto-scales.
/// </summary>
public sealed partial class MetricTileViewModel : ObservableObject
{
    private readonly RollingSeries _series = new();
    private readonly Func<double, string> _formatForTooltip;

    public MetricTileViewModel(string title, double maximum, Func<double, string> formatForTooltip)
    {
        Title = title;
        Maximum = maximum;
        _formatForTooltip = formatForTooltip;
    }

    public string Title { get; }

    /// <summary>Top of the Sparkline's scale; <see cref="double.NaN"/> auto-scales to the data.</summary>
    public double Maximum { get; }

    [ObservableProperty]
    private string _valueText = "n/a";

    [ObservableProperty]
    private string _detailText = "Not available on this PC";

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private IReadOnlyList<double> _values = [];

    [ObservableProperty]
    private string _tooltipText = "Last 60s - no data yet";

    /// <summary>Applies one tick's sample. A null <paramref name="sample"/> means the source is
    /// unavailable on this PC; the tile switches to its "n/a" state and stops updating its series.</summary>
    public void Update(double? sample, string valueText, string detailText)
    {
        if (sample is null)
        {
            IsAvailable = false;
            ValueText = "n/a";
            DetailText = "Not available on this PC";
            return;
        }

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
