using Porchlight.Core.Cleanup;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Cleanup;

/// <summary>One suggested personal file (big file or old download).</summary>
public sealed class CleanupFileRowViewModel
{
    public CleanupFileRowViewModel(FoundFile file)
    {
        File = file;
        SizeText = ByteFormatter.FormatBytes(file.Bytes);
        ModifiedText = CleanupTextFormatter.FormatModified(file.LastWriteUtc);
    }

    public FoundFile File { get; }

    public string Name => File.Name;

    public string Folder => File.Folder;

    public string SizeText { get; }

    public string ModifiedText { get; }
}
