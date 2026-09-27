using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Hardware;

namespace Porchlight.App.Features.Hardware;

/// <summary>
/// One group in the sensors tree (a piece of hardware, or a sub-part of one). S10: instances (and
/// their <see cref="Sensors"/>/<see cref="Children"/> instances) are kept alive and merged in place
/// every tick via <see cref="UpdateFrom"/> rather than rebuilt from scratch, so <see cref="IsExpanded"/>
/// - bound two-way from the tree's <c>TreeViewItem</c> style - and any other UI state survives the
/// once-a-second refresh instead of resetting every tick.
/// </summary>
public sealed partial class SensorTreeNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isVisible = true;

    public SensorTreeNodeViewModel(HardwareNode node)
    {
        Id = node.Id;
        UpdateFrom(node);
    }

    public string Id { get; }

    public ObservableCollection<SensorRowViewModel> Sensors { get; } = [];

    public ObservableCollection<SensorTreeNodeViewModel> Children { get; } = [];

    public void UpdateFrom(HardwareNode node)
    {
        Name = node.Name;
        MergeById(Sensors, node.Sensors, vm => vm.Id, s => s.Id, (vm, s) => vm.UpdateFrom(s), s => new SensorRowViewModel(s));
        MergeById(Children, node.Children, vm => vm.Id, c => c.Id, (vm, c) => vm.UpdateFrom(c), c => new SensorTreeNodeViewModel(c));
    }

    /// <summary>Applies the filter recursively: a row is visible if the filter is empty or its name
    /// matches; this node is visible if the filter is empty, its own name matches, or any
    /// descendant row/node is visible (S10: "filter at sensor level", not just whole-node).</summary>
    public bool ApplyFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            foreach (var sensor in Sensors)
            {
                sensor.IsVisible = true;
            }

            var anyChildVisible = false;
            foreach (var child in Children)
            {
                anyChildVisible |= child.ApplyFilter(filter);
            }

            IsVisible = true;
            return true;
        }

        var anySensorVisible = false;
        foreach (var sensor in Sensors)
        {
            sensor.IsVisible = sensor.Matches(filter);
            anySensorVisible |= sensor.IsVisible;
        }

        var anyChildMatches = false;
        foreach (var child in Children)
        {
            anyChildMatches |= child.ApplyFilter(filter);
        }

        var selfMatches = Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        IsVisible = selfMatches || anySensorVisible || anyChildMatches;

        if (selfMatches)
        {
            // The node's own name matched (e.g. filtering "CPU") - show all its sensors rather than
            // only the ones that happen to also match the text.
            foreach (var sensor in Sensors)
            {
                sensor.IsVisible = true;
            }
        }

        return IsVisible;
    }

    /// <summary>Merges <paramref name="source"/> into <paramref name="target"/> by id: existing
    /// items are updated in place (preserving identity/UI state), new items are added, and items no
    /// longer present are removed. Avoids the "clear and rebuild" pattern that would otherwise
    /// discard every <see cref="TreeViewItem"/>'s expansion state each tick.</summary>
    private static void MergeById<TViewModel, TSource>(
        ObservableCollection<TViewModel> target,
        IReadOnlyList<TSource> source,
        Func<TViewModel, string> getViewModelId,
        Func<TSource, string> getSourceId,
        Action<TViewModel, TSource> update,
        Func<TSource, TViewModel> create)
        where TViewModel : class
    {
        var byId = new Dictionary<string, TViewModel>(target.Count);
        foreach (var item in target)
        {
            byId[getViewModelId(item)] = item;
        }

        var seen = new HashSet<string>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            var sourceItem = source[i];
            var id = getSourceId(sourceItem);
            seen.Add(id);

            if (byId.TryGetValue(id, out var existing))
            {
                update(existing, sourceItem);
                var currentIndex = target.IndexOf(existing);
                if (currentIndex != i && i < target.Count)
                {
                    target.Move(currentIndex, i);
                }
            }
            else
            {
                var created = create(sourceItem);
                if (i < target.Count)
                {
                    target.Insert(i, created);
                }
                else
                {
                    target.Add(created);
                }
            }
        }

        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(getViewModelId(target[i])))
            {
                target.RemoveAt(i);
            }
        }
    }
}
