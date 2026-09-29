namespace Porchlight.Core.Cleanup;

/// <summary>A kind of junk the "Free up space" page can clean.</summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Title">Short plain-language title.</param>
/// <param name="Description">One plain sentence saying what this is and that it is safe to remove.</param>
/// <param name="RequiresAdmin">True if the whole category needs administrator rights (it is shown
/// disabled, with an explanation, while Porchlight is not elevated).</param>
/// <param name="IsSelectedByDefault">Whether it starts ticked.</param>
/// <param name="Roots">The folders whose contents are cleaned. Empty for the Recycle Bin, which is
/// handled through the shell API instead.</param>
/// <param name="FileNamePattern">Optional simple wildcard (e.g. <c>thumbcache_*.db</c>). When set, only
/// matching files directly inside each root are considered - nothing is recursed into.</param>
/// <param name="MinimumAge">Files modified more recently than this are left alone (an app may still
/// be using them).</param>
public sealed record CleanupCategory(
    CleanupCategoryId Id,
    string Title,
    string Description,
    bool RequiresAdmin,
    bool IsSelectedByDefault,
    IReadOnlyList<CleanupRoot> Roots,
    string? FileNamePattern,
    TimeSpan MinimumAge)
{
    /// <summary>True for the Recycle Bin, which has no folder roots and is measured/emptied through
    /// <see cref="IRecycleBin"/>.</summary>
    public bool IsRecycleBin => Id == CleanupCategoryId.RecycleBin;
}
