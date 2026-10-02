using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porchlight.App.Features.Cleanup;
using Porchlight.Core.Winget;

namespace Porchlight.App.Features.Updates;

/// <summary>The Updates page's history body: the stored entries grouped by day, plus "Clear history"
/// (which asks first). Owned by <see cref="UpdatesViewModel"/>. See
/// <c>docs/specs/26-update-history.md</c>.</summary>
public sealed partial class UpdateHistoryViewModel : ObservableObject
{
    public const string EmptyText = "Nothing here yet. Updates you run from Porchlight will show up here.";

    public const string ScopeText = "Updates run from Porchlight.";

    public const string ClearTitle = "Clear history";

    public const string ClearConfirmationText = "Clear all update history? This can't be undone.";

    private readonly IUpdateHistoryStore _store;
    private readonly IConfirmationDialog _confirmation;
    private readonly TimeProvider _timeProvider;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private bool _hasEntries;

    public UpdateHistoryViewModel(IUpdateHistoryStore store, IConfirmationDialog confirmation, TimeProvider timeProvider)
    {
        _store = store;
        _confirmation = confirmation;
        _timeProvider = timeProvider;
    }

    public ObservableCollection<UpdateHistoryGroupViewModel> Groups { get; } = [];

    public bool IsEmpty => !HasEntries;

    /// <summary>Reloads <see cref="Groups"/> from the store.</summary>
    public void Load()
    {
        var zone = _timeProvider.LocalTimeZone;
        var entries = _store.GetAll();
        var groups = UpdateHistoryLog.GroupByDay(entries, _timeProvider.GetUtcNow(), zone);

        Groups.Clear();
        foreach (var (label, items) in groups)
        {
            Groups.Add(new UpdateHistoryGroupViewModel(
                label, [.. items.Select(e => new UpdateHistoryEntryViewModel(e, zone))]));
        }

        HasEntries = Groups.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(HasEntries))]
    private void Clear()
    {
        if (!_confirmation.Confirm(ClearTitle, ClearConfirmationText))
        {
            return;
        }

        _store.Clear();
        Load();
    }
}
