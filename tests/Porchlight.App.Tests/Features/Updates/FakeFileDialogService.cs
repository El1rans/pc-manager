using Porchlight.App.Shell;

namespace Porchlight.App.Tests.Features.Updates;

/// <summary>Fake <see cref="IFileDialogService"/>: returns the configured paths instead of showing a dialog.</summary>
internal sealed class FakeFileDialogService : IFileDialogService
{
    public string? SavePath { get; set; }

    public string? OpenPath { get; set; }

    public string? LastDefaultFileName { get; private set; }

    public string? LastInitialDirectory { get; private set; }

    public string? PickSaveFile(string title, string defaultFileName, string filter, string initialDirectory)
    {
        LastDefaultFileName = defaultFileName;
        LastInitialDirectory = initialDirectory;
        return SavePath;
    }

    public string? PickOpenFile(string title, string filter) => OpenPath;
}
