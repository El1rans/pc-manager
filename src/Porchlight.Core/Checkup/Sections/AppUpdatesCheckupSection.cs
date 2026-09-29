using System.Globalization;
using Porchlight.Core.Winget;

namespace Porchlight.Core.Checkup.Sections;

/// <summary>How many app updates were waiting at the last check. Only a count - never app names -
/// and never starts a new winget run.</summary>
public sealed class AppUpdatesCheckupSection : ICheckupSection
{
    private readonly IPendingUpdatesTracker _tracker;

    public AppUpdatesCheckupSection(IPendingUpdatesTracker tracker)
    {
        _tracker = tracker;
    }

    public string Title => "App updates";

    public int Order => 400;

    public Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken)
    {
        CheckupSectionResult result;
        if (_tracker.Current is not { } status)
        {
            result = new CheckupSectionResult(Title, CheckupSeverity.Ok, ["Porchlight hasn't checked for app updates yet."]);
        }
        else
        {
            var when = status.CheckedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            if (status.Count == 0)
            {
                result = new CheckupSectionResult(Title, CheckupSeverity.Ok, [$"All apps were up to date at the last check ({when})."]);
            }
            else
            {
                var noun = status.Count == 1 ? "app has an update" : "apps have updates";
                result = new CheckupSectionResult(
                    Title, CheckupSeverity.NeedsAttention, [$"{status.Count} {noun} waiting (last checked {when})."]);
            }
        }

        return Task.FromResult<CheckupSectionResult?>(result);
    }
}
