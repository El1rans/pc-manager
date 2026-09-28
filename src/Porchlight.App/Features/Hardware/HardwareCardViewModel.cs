using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Hardware;

namespace Porchlight.App.Features.Hardware;

/// <summary>
/// One card on the sensors tab: a piece of hardware (spec 10: "One card per hardware device
/// (Expander, header = device name + type icon; CPU and GPU expanded by default, others
/// collapsed)"). A card's own sensors and every descendant <see cref="HardwareNode"/>'s sensors
/// (e.g. per-core sub-groups) are flattened together and grouped into <see cref="Sections"/> by
/// <see cref="SensorType"/>, per <see cref="SensorSectionOrder"/> - the maintainer's device is a
/// single physical part regardless of how LHM happens to nest its sensors internally.
/// </summary>
public sealed partial class HardwareCardViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isVisible = true;

    public HardwareCardViewModel(HardwareNode node, UnusedSensorTracker unusedTracker)
    {
        Id = node.Id;
        Type = node.Type;
        IsExpanded = Type is HardwareNodeType.Cpu or HardwareNodeType.Gpu;
        UpdateFrom(node, unusedTracker);
    }

    public string Id { get; }

    public HardwareNodeType Type { get; }

    public string Icon => Type switch
    {
        HardwareNodeType.Cpu => "",
        HardwareNodeType.Gpu => "",
        HardwareNodeType.Motherboard => "",
        HardwareNodeType.Memory => "",
        HardwareNodeType.Storage => "",
        HardwareNodeType.Network => "",
        _ => "",
    };

    public ObservableCollection<SensorSectionViewModel> Sections { get; } = [];

    public void UpdateFrom(HardwareNode node, UnusedSensorTracker unusedTracker)
    {
        Name = node.Name;

        var existingRowsById = new Dictionary<string, SensorRowViewModel>();
        foreach (var section in Sections)
        {
            foreach (var row in section.Sensors)
            {
                existingRowsById[row.Id] = row;
            }
        }

        var grouped = FlattenSensors(node)
            .GroupBy(s => s.Type)
            .OrderBy(g => SensorSectionOrder.RankOf(g.Key))
            .ToList();

        var seenTypes = new HashSet<SensorType>(grouped.Count);
        for (var i = 0; i < grouped.Count; i++)
        {
            var group = grouped[i];
            seenTypes.Add(group.Key);

            var section = Sections.FirstOrDefault(s => s.Type == group.Key);
            if (section is null)
            {
                section = new SensorSectionViewModel(group.Key);
                if (i < Sections.Count)
                {
                    Sections.Insert(i, section);
                }
                else
                {
                    Sections.Add(section);
                }
            }
            else
            {
                var currentIndex = Sections.IndexOf(section);
                if (currentIndex != i)
                {
                    Sections.Move(currentIndex, i);
                }
            }

            section.UpdateFrom(group.ToList(), unusedTracker, existingRowsById);
        }

        for (var i = Sections.Count - 1; i >= 0; i--)
        {
            if (!seenTypes.Contains(Sections[i].Type))
            {
                Sections.RemoveAt(i);
            }
        }
    }

    /// <summary>Recomputes visibility for every section/row from the filter text and "Hide unused
    /// sensors" toggle. A device whose own name matches the filter shows every sensor under it,
    /// matching the previous tree's "filter at sensor level, but a matching group name shows
    /// everything in it" behaviour (S10).</summary>
    public void ApplyFilter(string filter, bool hideUnused)
    {
        var selfMatches = string.IsNullOrEmpty(filter) || Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        var anySectionVisible = false;
        foreach (var section in Sections)
        {
            anySectionVisible |= section.ApplyFilter(filter, hideUnused, selfMatches);
        }

        IsVisible = selfMatches || anySectionVisible;
    }

    private static IEnumerable<SensorReading> FlattenSensors(HardwareNode node)
    {
        foreach (var sensor in node.Sensors)
        {
            yield return sensor;
        }

        foreach (var child in node.Children)
        {
            foreach (var sensor in FlattenSensors(child))
            {
                yield return sensor;
            }
        }
    }
}
