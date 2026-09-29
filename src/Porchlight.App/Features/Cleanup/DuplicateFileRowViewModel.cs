using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Cleanup;

namespace Porchlight.App.Features.Cleanup;

/// <summary>One copy in a duplicate group, with the checkbox that chooses it for the Recycle Bin.</summary>
public sealed partial class DuplicateFileRowViewModel : ObservableObject
{
    private readonly DuplicateGroupViewModel _group;
    private bool _silent;

    internal DuplicateFileRowViewModel(DuplicateFile file, bool isNewest, DuplicateGroupViewModel group)
    {
        File = file;
        IsNewest = isNewest;
        _group = group;
        ModifiedText = CleanupTextFormatter.FormatModified(file.LastWriteUtc);
    }

    public DuplicateFile File { get; }

    public string Name => File.Name;

    public string Folder => File.Folder;

    public string ModifiedText { get; }

    /// <summary>The copy "Keep newest" would keep; labelled with text, not colour.</summary>
    public bool IsNewest { get; }

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>False for the last unticked copy of a group: it can't be ticked, so one copy always stays.</summary>
    public bool CanTick => IsSelected || _group.Files.Any(other => !ReferenceEquals(other, this) && !other.IsSelected);

    /// <summary>True when this is the only copy left unticked ("This copy will stay").</summary>
    public bool IsLastCopy => !IsSelected && !CanTick;

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_silent)
        {
            _group.OnRowSelectionChanged(this, value);
        }
    }

    /// <summary>Sets the tick without going through the "keep one copy" guard.</summary>
    internal void SetSelectedSilently(bool value)
    {
        _silent = true;
        try
        {
            IsSelected = value;
        }
        finally
        {
            _silent = false;
        }
    }

    internal void RefreshGuard()
    {
        OnPropertyChanged(nameof(CanTick));
        OnPropertyChanged(nameof(IsLastCopy));
    }
}
