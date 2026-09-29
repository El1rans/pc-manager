namespace Porchlight.App.Features.Cleanup;

/// <summary>Lets the user pick a folder, behind an interface so view model tests never show a real
/// dialog.</summary>
public interface IFolderPicker
{
    /// <summary>Shows the standard folder dialog. Returns the chosen folder, or null if the user
    /// cancelled.</summary>
    string? PickFolder(string? initialFolder);
}
