using Microsoft.Win32;

namespace Porchlight.App.Shell;

/// <inheritdoc cref="IFileDialogService"/>
public sealed class FileDialogService : IFileDialogService
{
    public string? PickSaveFile(string title, string defaultFileName, string filter, string initialDirectory)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = defaultFileName,
            Filter = filter,
            InitialDirectory = initialDirectory,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog(System.Windows.Application.Current?.MainWindow) == true ? dialog.FileName : null;
    }

    public string? PickOpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
        };

        return dialog.ShowDialog(System.Windows.Application.Current?.MainWindow) == true ? dialog.FileName : null;
    }
}
