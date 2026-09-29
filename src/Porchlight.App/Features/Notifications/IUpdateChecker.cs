using Porchlight.App.Features.Updates;

namespace Porchlight.App.Features.Notifications;

/// <summary>Runs the Updates page's existing winget check. Only checks; never installs.</summary>
public interface IUpdateChecker
{
    /// <summary>Checks for updates and reports the typed outcome: succeeded with the number of
    /// (not ignored) updates, failed, or skipped (busy).</summary>
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken);
}
