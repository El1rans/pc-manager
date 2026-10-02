using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Features.Updates;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.App.Tests.Features.Updates;

public sealed class UpdateHistoryViewModelTests
{
    private readonly FakeUpdateHistoryStore _store = new();
    private readonly FakeHistoryConfirmation _confirmation = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

    public UpdateHistoryViewModelTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
    }

    private UpdateHistoryViewModel Create() => new(_store, _confirmation, _time);

    private static UpdateHistoryEntry Entry(
        DateTimeOffset when, string name = "Zoom", string? from = "6.1.0", string? to = "6.2.1",
        bool succeeded = true, string title = "Updated", string explanation = "",
        UpdateHistoryAction action = UpdateHistoryAction.Update) =>
        new()
        {
            TimestampUtc = when, PackageId = name, PackageName = name, FromVersion = from, ToVersion = to,
            Action = action, Succeeded = succeeded, OutcomeTitle = title, Explanation = explanation,
        };

    [Fact]
    public void Load_NoEntries_IsEmpty()
    {
        var viewModel = Create();

        viewModel.Load();

        Assert.True(viewModel.IsEmpty);
        Assert.Empty(viewModel.Groups);
        Assert.False(viewModel.ClearCommand.CanExecute(null));
    }

    [Fact]
    public void Load_GroupsByDayNewestFirst()
    {
        _store.Add(Entry(new DateTimeOffset(2026, 9, 29, 9, 0, 0, TimeSpan.Zero), "Yday"));
        _store.Add(Entry(new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero), "Early"));
        _store.Add(Entry(new DateTimeOffset(2026, 9, 30, 11, 0, 0, TimeSpan.Zero), "Late"));
        var viewModel = Create();

        viewModel.Load();

        Assert.False(viewModel.IsEmpty);
        Assert.Equal(["Today", "Yesterday"], viewModel.Groups.Select(g => g.Label));
        Assert.Equal(["Late", "Early"], viewModel.Groups[0].Entries.Select(e => e.PackageName));
    }

    [Fact]
    public void Clear_AfterConfirm_EmptiesStoreAndList()
    {
        _store.Add(Entry(_time.GetUtcNow()));
        var viewModel = Create();
        viewModel.Load();

        viewModel.ClearCommand.Execute(null);

        Assert.Equal(UpdateHistoryViewModel.ClearConfirmationText, _confirmation.LastMessage);
        Assert.Empty(_store.GetAll());
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public void Clear_WhenCancelled_KeepsEverything()
    {
        _store.Add(Entry(_time.GetUtcNow()));
        _confirmation.Answer = false;
        var viewModel = Create();
        viewModel.Load();

        viewModel.ClearCommand.Execute(null);

        Assert.Equal(1, _confirmation.Calls);
        Assert.Single(_store.GetAll());
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public void EntryViewModel_FormatsSuccessRow()
    {
        var row = new UpdateHistoryEntryViewModel(Entry(new DateTimeOffset(2026, 9, 30, 14, 5, 0, TimeSpan.Zero)), TimeZoneInfo.Utc);

        Assert.Equal("6.1.0 → 6.2.1", row.VersionText);
        Assert.Equal("Updated", row.ActionText);
        Assert.Equal("Done", row.ResultText);
        Assert.Equal(string.Empty, row.Explanation);
        Assert.Equal("14:05", row.TimeText);
        Assert.Equal("Zoom, updated to 6.2.1, done, 14:05", row.AutomationName);
    }

    [Fact]
    public void EntryViewModel_UnknownOldVersion_ShowsOnlyNewVersion()
    {
        var row = new UpdateHistoryEntryViewModel(Entry(_time.GetUtcNow(), from: null), TimeZoneInfo.Utc);

        Assert.Equal("6.2.1", row.VersionText);
    }

    [Fact]
    public void EntryViewModel_FailedRow_ShowsOutcomeTitleAndExplanationWithCode()
    {
        var entry = Entry(
            _time.GetUtcNow(), succeeded: false, title: "Needs a reinstall", explanation: "Reinstall it.",
            action: UpdateHistoryAction.Reinstall);
        entry.ExitCode = unchecked((int)0x8A15008E);

        var row = new UpdateHistoryEntryViewModel(entry, TimeZoneInfo.Utc);

        Assert.Equal("Reinstalled", row.ActionText);
        Assert.Equal("Didn't work - Needs a reinstall", row.ResultText);
        Assert.Equal("Reinstall it. (winget code 0x8A15008E)", row.Explanation);
        Assert.False(row.Succeeded);
    }

    [Fact]
    public void EntryViewModel_ConvertsTimeToTheZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");

        var row = new UpdateHistoryEntryViewModel(Entry(new DateTimeOffset(2026, 9, 30, 22, 30, 0, TimeSpan.Zero)), zone);

        Assert.Equal("00:30", row.TimeText);
    }
}
