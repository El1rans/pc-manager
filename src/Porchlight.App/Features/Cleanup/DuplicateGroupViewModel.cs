using CommunityToolkit.Mvvm.Input;
using Porchlight.Core.Cleanup;

namespace Porchlight.App.Features.Cleanup;

/// <summary>A set of identical files and the ticks that choose which copies to move to the Recycle
/// Bin. The UI never lets every copy be ticked (Core enforces this too).</summary>
public sealed partial class DuplicateGroupViewModel
{
    private readonly Action _selectionChanged;
    private readonly Action _lastCopyRefused;

    public DuplicateGroupViewModel(DuplicateGroup group, Action selectionChanged, Action lastCopyRefused)
    {
        Group = group;
        _selectionChanged = selectionChanged;
        _lastCopyRefused = lastCopyRefused;
        var newest = DuplicateSelection.PickNewest(group);
        Files = group.Files.Select(file => new DuplicateFileRowViewModel(file, ReferenceEquals(file, newest), this)).ToList();
        Title = DiskInsightsTextFormatter.FormatGroupTitle(group);
        WastedText = DiskInsightsTextFormatter.FormatWasted(group.WastedBytes);
    }

    public DuplicateGroup Group { get; }

    public IReadOnlyList<DuplicateFileRowViewModel> Files { get; }

    public string Title { get; }

    public string WastedText { get; }

    public IEnumerable<DuplicateFileRowViewModel> SelectedFiles => Files.Where(file => file.IsSelected);

    /// <summary>"Keep newest": ticks every copy except the newest one.</summary>
    [RelayCommand]
    private void KeepNewest() => ApplyKeepNewest();

    internal void ApplyKeepNewest()
    {
        foreach (var file in Files)
        {
            file.SetSelectedSilently(!file.IsNewest);
        }

        Notify();
    }

    internal void ClearSelection()
    {
        foreach (var file in Files)
        {
            file.SetSelectedSilently(false);
        }

        Notify();
    }

    internal void OnRowSelectionChanged(DuplicateFileRowViewModel row, bool selected)
    {
        if (selected && Files.All(file => file.IsSelected))
        {
            // Ticking the last unticked copy would leave nothing. The checkbox is already disabled
            // in that state, so this is only a backstop for a programmatic change.
            row.SetSelectedSilently(false);
            Notify();
            _lastCopyRefused();
            return;
        }

        Notify();
    }

    private void Notify()
    {
        foreach (var file in Files)
        {
            file.RefreshGuard();
        }

        _selectionChanged();
    }
}
