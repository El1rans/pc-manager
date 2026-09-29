using Microsoft.Win32;

namespace Porchlight.App.Features.Lighting;

/// <inheritdoc cref="IAnimationFilePicker"/>
public sealed class AnimationFilePicker : IAnimationFilePicker
{
    public string? PickFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import a custom animation",
            Filter = "Porchlight animation (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
