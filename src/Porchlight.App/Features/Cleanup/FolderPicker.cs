using System.IO;
using Microsoft.Win32;

namespace Porchlight.App.Features.Cleanup;

/// <inheritdoc cref="IFolderPicker"/>
public sealed class FolderPicker : IFolderPicker
{
    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a folder to look at",
        };
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        var owner = System.Windows.Application.Current?.MainWindow;
        var chosen = owner is { IsVisible: true } ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        return chosen == true && !string.IsNullOrWhiteSpace(dialog.FolderName) ? dialog.FolderName : null;
    }
}
