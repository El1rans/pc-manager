using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Hardware;

namespace Porchlight.App.Features.Hardware;

/// <summary>
/// One sensor-type section inside a <see cref="HardwareCardViewModel"/> (spec 10: "Inside each
/// card, sections by sensor type"). Empty (or fully filtered-out) sections are hidden via
/// <see cref="IsVisible"/> rather than removed, so the card's layout does not jump every tick.
/// </summary>
public sealed partial class SensorSectionViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible = true;

    public SensorSectionViewModel(SensorType type)
    {
        Type = type;
        Heading = SensorSectionOrder.Heading(type);
        Unit = SensorFormatter.Unit(type);
    }

    public SensorType Type { get; }

    public string Heading { get; }

    public string Unit { get; }

    public ObservableCollection<SensorRowViewModel> Sensors { get; } = [];

    /// <summary>Merges this tick's readings for this section's sensor type into <see cref="Sensors"/>
    /// in place (S10 pattern), preferring an existing row (from <paramref name="existingRowsById"/>,
    /// which may belong to a different section if a sensor's type ever changed) over creating a new
    /// one, so a row's identity - and hence any bound UI state - survives across ticks.</summary>
    public void UpdateFrom(
        IReadOnlyList<SensorReading> readings,
        UnusedSensorTracker unusedTracker,
        Dictionary<string, SensorRowViewModel> existingRowsById)
    {
        var seen = new HashSet<string>(readings.Count);
        for (var i = 0; i < readings.Count; i++)
        {
            var reading = readings[i];
            seen.Add(reading.Id);
            var isUsed = unusedTracker.Observe(reading);

            if (existingRowsById.TryGetValue(reading.Id, out var row))
            {
                row.UpdateFrom(reading, isUsed);
                var currentIndex = Sensors.IndexOf(row);
                if (currentIndex < 0)
                {
                    // Row existed (in another section) but not here yet - a sensor's type does not
                    // change in practice, but this keeps the merge correct if it ever did.
                    if (i < Sensors.Count)
                    {
                        Sensors.Insert(i, row);
                    }
                    else
                    {
                        Sensors.Add(row);
                    }
                }
                else if (currentIndex != i)
                {
                    Sensors.Move(currentIndex, i);
                }
            }
            else
            {
                row = new SensorRowViewModel(reading, isUsed);
                if (i < Sensors.Count)
                {
                    Sensors.Insert(i, row);
                }
                else
                {
                    Sensors.Add(row);
                }
            }
        }

        for (var i = Sensors.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(Sensors[i].Id))
            {
                Sensors.RemoveAt(i);
            }
        }
    }

    /// <summary>Recomputes visibility for every row and this section itself. Returns whether the
    /// section has at least one visible row (used by the card to decide its own visibility).</summary>
    public bool ApplyFilter(string filter, bool hideUnused, bool ancestorMatched)
    {
        var anyVisible = false;
        foreach (var sensor in Sensors)
        {
            sensor.ApplyVisibility(filter, hideUnused, ancestorMatched);
            anyVisible |= sensor.IsVisible;
        }

        IsVisible = anyVisible;
        return anyVisible;
    }
}
