namespace Porchlight.App.Shell;

/// <summary>The standard Windows open/save file dialogs, behind an interface so view models that
/// save or load a file are unit-testable without showing a real dialog.</summary>
public interface IFileDialogService
{
    /// <summary>Shows a "save as" dialog. Returns the chosen path, or null if the user cancelled.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="defaultFileName">Suggested file name.</param>
    /// <param name="filter">Windows filter string, e.g. <c>"Web page (*.html)|*.html"</c>.</param>
    /// <param name="initialDirectory">Folder the dialog opens in.</param>
    string? PickSaveFile(string title, string defaultFileName, string filter, string initialDirectory);

    /// <summary>Shows an "open" dialog. Returns the chosen path, or null if the user cancelled.</summary>
    string? PickOpenFile(string title, string filter);
}
