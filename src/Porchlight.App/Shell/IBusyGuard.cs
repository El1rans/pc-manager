namespace Porchlight.App.Shell;

/// <summary>
/// Implemented by a page's view model when it can be doing background work the user would lose
/// track of if the app closed right now (e.g. an in-flight winget upgrade that would keep running,
/// unattended, after the window disappears). <see cref="MainWindow"/>'s Closing handler asks the
/// user to confirm before closing when any registered <see cref="IPage"/> reports
/// <see cref="IsBusyWithWork"/> - it never cancels or kills the work itself, only the window close.
/// </summary>
public interface IBusyGuard
{
    /// <summary>True while this page has work in flight that the user should be warned about
    /// losing sight of before the app closes.</summary>
    bool IsBusyWithWork { get; }

    /// <summary>The confirmation message shown when <see cref="IsBusyWithWork"/> is true and the
    /// user tries to close the app - should explain what is running and what closing does (or does
    /// not) stop.</summary>
    string BusyMessage { get; }
}
