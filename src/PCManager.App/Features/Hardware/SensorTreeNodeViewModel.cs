using PCManager.Core.Hardware;

namespace PCManager.App.Features.Hardware;

/// <summary>One group in the sensors tree (a piece of hardware, or a sub-part of one).</summary>
public sealed class SensorTreeNodeViewModel
{
    public SensorTreeNodeViewModel(HardwareNode node)
    {
        Name = node.Name;
        Sensors = [.. node.Sensors.Select(s => new SensorRowViewModel(s))];
        Children = [.. node.Children.Select(c => new SensorTreeNodeViewModel(c))];
    }

    public string Name { get; }

    public IReadOnlyList<SensorRowViewModel> Sensors { get; }

    public IReadOnlyList<SensorTreeNodeViewModel> Children { get; }

    /// <summary>Whether this node or any descendant sensor's name matches the filter text
    /// (case-insensitive substring). Used by the sensors tab's filter box.</summary>
    public bool Matches(string filter) =>
        Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        Sensors.Any(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
        Children.Any(c => c.Matches(filter));
}
