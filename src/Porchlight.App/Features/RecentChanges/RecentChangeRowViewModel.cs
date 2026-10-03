using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Changes;

namespace Porchlight.App.Features.RecentChanges;

/// <summary>One row on the Recent changes page.</summary>
public sealed partial class RecentChangeRowViewModel : ObservableObject
{
    public const string CannotUndoText = "Can't be undone";

    public const string UndoneText = "Undone";

    private const string TimeFormat = "d MMM yyyy, HH:mm";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUndo), nameof(ShowCannotUndo), nameof(ShowUndone))]
    private bool _isBusy;

    public RecentChangeRowViewModel(ChangeEntry entry)
    {
        Entry = entry;
    }

    public ChangeEntry Entry { get; }

    public Guid Id => Entry.Id;

    public string Description => Entry.Description;

    public string AreaText => Entry.Area switch
    {
        ChangeArea.Startup => "Startup apps",
        ChangeArea.Services => "Services",
        ChangeArea.Cleanup => "Free up space",
        ChangeArea.Apps => "Apps",
        ChangeArea.Updates => "Updates",
        _ => "Other",
    };

    /// <summary>The time in the PC's local time zone, in English day and month names like the rest of the app.</summary>
    public string TimeText => Entry.Time.ToLocalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);

    public bool CanUndo => Entry.CanUndo && !IsBusy;

    public bool ShowCannotUndo => Entry.UndoType is null;

    public bool ShowUndone => Entry.UndoneAt is not null;

    public string UndoAutomationName => $"Undo: {Entry.Description}";
}
