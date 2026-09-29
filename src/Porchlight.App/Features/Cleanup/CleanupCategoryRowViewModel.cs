using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Cleanup;

/// <summary>One tickable category row on the "Clean up" card.</summary>
public sealed partial class CleanupCategoryRowViewModel : ObservableObject
{
    private readonly Action<CleanupCategoryRowViewModel>? _selectionChanged;

    public CleanupCategoryRowViewModel(
        CleanupCategory category, bool isSelected, bool isEnabled, Action<CleanupCategoryRowViewModel>? selectionChanged)
    {
        Category = category;
        _isSelected = isSelected && isEnabled;
        _isEnabled = isEnabled;
        _selectionChanged = selectionChanged;
        _note = isEnabled ? string.Empty : "Restart as administrator to include this.";
    }

    public CleanupCategory Category { get; }

    public string Title => Category.Title;

    public string Description => Category.Description;

    public long Bytes { get; private set; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _sizeText = "Waiting...";

    /// <summary>Extra plain-language line: needs administrator, or a browser to close.</summary>
    [ObservableProperty]
    private string _note;

    partial void OnIsSelectedChanged(bool value) => _selectionChanged?.Invoke(this);

    public void ApplyScan(CleanupCategoryScan scan)
    {
        Bytes = scan.Bytes;
        SizeText = IsEnabled ? ByteFormatter.FormatBytes(scan.Bytes) : "Not scanned";
        if (IsEnabled)
        {
            Note = scan.BlockedPrograms.Count > 0
                ? string.Join(' ', scan.BlockedPrograms.Select(p => $"Close {p} to clean its cache."))
                : string.Empty;
        }
    }
}
