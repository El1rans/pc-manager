using Porchlight.App.Features.Cleanup;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Processes;

namespace Porchlight.App.Tests.Features.Cleanup;

/// <summary>Fakes shared by the disk map and duplicate finder view model tests.</summary>
internal sealed class FakeDiskMapper : IDiskSpaceMapper
{
    public DiskNode? Result { get; set; }

    public Exception? Throw { get; set; }

    public string? LastRoot { get; private set; }

    public Task<DiskNode> MapAsync(string root, IProgress<DiskMapProgress>? progress, CancellationToken cancellationToken)
    {
        LastRoot = root;
        return Throw is null ? Task.FromResult(Result!) : Task.FromException<DiskNode>(Throw);
    }
}

internal sealed class FakeInsightsPaths : ICleanupPathProvider
{
    public string TempPath => @"C:\T";

    public string WindowsDirectory => @"C:\Windows";

    public string LocalAppData => @"C:\U\AppData\Local";

    public string ProgramData => @"C:\ProgramData";

    public string UserProfile => @"C:\Users\Test";

    public string ProgramFiles => @"C:\Program Files";

    public string ProgramFilesX86 => @"C:\Program Files (x86)";

    public string DownloadsFolder => @"C:\Users\Test\Downloads";

    public IReadOnlyList<string> PersonalFolders => [@"C:\Users\Test\Documents"];
}

internal sealed class FakeInsightsDrives : IDriveMonitor
{
    public IReadOnlyList<DriveSnapshot> GetDrives() =>
        [new(@"C:\", "Windows", "NTFS", 1000, 400, false), new(@"D:\", null, "NTFS", 1000, 900, false)];
}

internal sealed class FakeInsightsRecycler : IRecycler
{
    public bool Succeeds { get; set; } = true;

    public List<string> Moved { get; } = [];

    public bool MoveToRecycleBin(string path)
    {
        Moved.Add(path);
        return Succeeds;
    }
}

internal sealed class FakeInsightsProcessRunner : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Arguments)> Detached { get; } = [];

    public Task<ProcessRunResult> RunAsync(
        string fileName, IReadOnlyList<string> arguments, IProgress<string>? onLine, IProgress<string>? onProgress,
        CancellationToken cancellationToken) => Task.FromResult(new ProcessRunResult(0, [], []));

    public void StartDetached(string fileName, IReadOnlyList<string> arguments) => Detached.Add((fileName, arguments));
}

internal sealed class FakeInsightsConfirmation : IConfirmationDialog
{
    public bool Answer { get; set; } = true;

    public int AskCount { get; private set; }

    public string LastMessage { get; private set; } = string.Empty;

    public bool Confirm(string title, string message)
    {
        AskCount++;
        LastMessage = message;
        return Answer;
    }
}

internal sealed class FakeFolderPicker : IFolderPicker
{
    public string? Choice { get; set; }

    public string? PickFolder(string? initialFolder) => Choice;
}

internal sealed class FakeDuplicateFinder : IDuplicateFinder
{
    public IReadOnlyList<DuplicateGroup> Groups { get; set; } = [];

    public Task<IReadOnlyList<DuplicateGroup>> FindAsync(
        DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress, CancellationToken cancellationToken) =>
        Task.FromResult(Groups);
}

internal sealed class FakeDuplicateRemover : IDuplicateRemover
{
    public List<string> LastSelected { get; } = [];

    public IReadOnlyList<string>? RemovedOverride { get; set; }

    public Task<DuplicateRemoveResult> RemoveAsync(
        IReadOnlyList<DuplicateGroup> groups, IReadOnlyCollection<string> selectedPaths, CancellationToken cancellationToken)
    {
        LastSelected.AddRange(selectedPaths);
        var removed = RemovedOverride ?? selectedPaths.ToList();
        return Task.FromResult(new DuplicateRemoveResult(removed.Count, removed.Count * 1000L, 0, 0, 0, false, removed));
    }
}
