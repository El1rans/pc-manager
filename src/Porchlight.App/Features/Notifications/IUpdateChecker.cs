namespace Porchlight.App.Features.Notifications;

/// <summary>Runs the Updates page's existing winget check. Only checks; never installs.</summary>
public interface IUpdateChecker
{
    /// <summary>Checks for updates and returns how many (not ignored) are available, or null when
    /// the check could not run (an update run or another check is in progress, winget is missing,
    /// or it failed).</summary>
    Task<int?> CheckAsync(CancellationToken cancellationToken);
}
