namespace Porchlight.App.Features.Lighting;

/// <summary>
/// The "Import animation file..." open-file dialog, behind an interface so the Lighting page's
/// import command is unit-testable without showing a real window.
/// </summary>
public interface IAnimationFilePicker
{
    /// <summary>Asks the user to pick an animation file. Returns its full path, or null if they
    /// cancelled.</summary>
    string? PickFile();
}
