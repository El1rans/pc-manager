using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porchlight.App.Shell;
using Porchlight.Core.Changes;

namespace Porchlight.App.Features.RecentChanges;

/// <summary>"Recent changes" page: what Porchlight changed on this PC, newest first, with Undo where
/// it is possible. See docs/specs/34-recent-changes.md.</summary>
public sealed partial class RecentChangesViewModel : PageViewModelBase
{
    private readonly IChangeJournal _journal;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _errorMessage;

    public RecentChangesViewModel(IChangeJournal journal)
    {
        _journal = journal;
    }

    public override string Title => "Recent changes";

    // Segoe Fluent Icons "History".
    public override string Glyph => "";

    // Last in the Tune-up group (after Startup 2, Cleanup 3 and Health 4).
    public override int Order => 5;

    public override PageCategory Category => PageCategory.TuneUp;

    public ObservableCollection<RecentChangeRowViewModel> Changes { get; } = [];

    public bool ShowEmptyState => Changes.Count == 0;

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        Message = null;
        ErrorMessage = null;
        Reload();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task UndoAsync(RecentChangeRowViewModel? row)
    {
        if (row is null || !row.CanUndo)
        {
            return;
        }

        Message = null;
        ErrorMessage = null;
        row.IsBusy = true;
        try
        {
            var result = await _journal.UndoAsync(row.Id, CancellationToken.None);
            if (result.Succeeded)
            {
                Message = result.Message;
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        finally
        {
            row.IsBusy = false;
            Reload();
        }
    }

    private void Reload()
    {
        Changes.Clear();
        foreach (var entry in _journal.GetAll())
        {
            Changes.Add(new RecentChangeRowViewModel(entry));
        }

        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
