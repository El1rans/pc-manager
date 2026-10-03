using Porchlight.Core.Backup;
using Xunit;

namespace Porchlight.Core.Tests.Backup;

public sealed class BackupToolMatcherTests
{
    [Fact]
    public void FindsKnownTools_SortedAndDistinct()
    {
        var result = BackupToolMatcher.Find(
        [
            "Veeam Agent for Microsoft Windows", "Dropbox", "Notepad++", "Macrium Reflect Free", "dropbox",
            "Backblaze", "Google Drive", "Acronis True Image",
        ]);

        Assert.Equal(["Acronis", "Backblaze", "Dropbox", "Google Drive", "Macrium Reflect", "Veeam Agent"], result);
    }

    [Fact]
    public void NothingKnown_IsEmpty() => Assert.Empty(BackupToolMatcher.Find(["Notepad++", "7-Zip"]));

    [Fact]
    public async Task Detector_UsesTheInstalledAppsReader()
    {
        var detector = new BackupToolDetector(new FakeInstalledApps("Dropbox", "Chrome"));

        Assert.Equal(["Dropbox"], detector.Find());
        await Task.CompletedTask;
    }

    private sealed class FakeInstalledApps(params string[] names) : Porchlight.Core.Cleanup.IInstalledAppsReader
    {
        public IReadOnlyList<Porchlight.Core.Cleanup.InstalledApp> GetInstalledApps() =>
            [.. names.Select(n => new Porchlight.Core.Cleanup.InstalledApp(n, null, null, null, null, "x", false))];
    }
}
