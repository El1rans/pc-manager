namespace Porchlight.App.Features.Setup;

/// <summary>
/// Shows the first-run "Choose what to set up" dialog. Used both automatically on first launch
/// and on demand from the sidebar footer's "Set up optional features" button, so both paths share
/// exactly one dialog implementation.
/// </summary>
public interface ISetupLauncher
{
    /// <summary>Shows the setup dialog modally over the main window and returns once it is closed.</summary>
    void ShowSetup();
}
