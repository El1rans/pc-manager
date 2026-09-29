using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Startup;

namespace Porchlight.App.Features.Startup;

/// <summary>One row of the Startup apps page.</summary>
public sealed partial class StartupEntryViewModel : ObservableObject
{
    private const string OnGlyph = "";
    private const string OffGlyph = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(StatusGlyph), nameof(ToggleLabel), nameof(ToggleAutomationName))]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChange), nameof(ShowNeedsAdmin))]
    private bool _isElevated;

    public StartupEntryViewModel(StartupEntry entry, bool isElevated)
    {
        Entry = entry;
        _isEnabled = entry.IsEnabled;
        _isElevated = isElevated;
    }

    public StartupEntry Entry { get; }

    public string Id => Entry.Id;

    public string Name => Entry.DisplayName;

    public string Publisher => string.IsNullOrWhiteSpace(Entry.Publisher) ? "Unknown publisher" : Entry.Publisher;

    public string SourceLabel => Entry.Source.ToLabel();

    public string Hint => Entry.Hint;

    public bool RecommendedToKeep => Entry.RecommendedToKeep;

    public string StatusText => IsEnabled ? "On" : "Off";

    public string StatusGlyph => IsEnabled ? OnGlyph : OffGlyph;

    public string ToggleLabel => IsEnabled ? "Turn off" : "Turn on";

    public string ToggleAutomationName => $"{ToggleLabel} {Name}";

    public bool CanChange => !Entry.RequiresAdmin || IsElevated;

    public bool ShowNeedsAdmin => !CanChange;
}
