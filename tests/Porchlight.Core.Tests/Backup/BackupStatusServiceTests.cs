using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Backup;
using Xunit;

namespace Porchlight.Core.Tests.Backup;

public sealed class BackupStatusServiceTests
{
    private sealed class FakeHistory(Func<FileHistoryStatus> read) : IFileHistoryReader
    {
        public FileHistoryStatus Read() => read();
    }

    private sealed class FakeOneDrive(Func<OneDriveStatus> read) : IOneDriveReader
    {
        public OneDriveStatus Read() => read();
    }

    private sealed class FakeTools(Func<IReadOnlyList<string>> find) : IBackupToolDetector
    {
        public IReadOnlyList<string> Find() => find();
    }

    private static BackupStatusService Create(
        Func<FileHistoryStatus> history, Func<OneDriveStatus> oneDrive, Func<IReadOnlyList<string>>? tools = null) =>
        new(new FakeHistory(history), new FakeOneDrive(oneDrive), new FakeTools(tools ?? (() => [])),
            NullLogger<BackupStatusService>.Instance);

    [Fact]
    public async Task AllSourcesWork_ReturnsEverything()
    {
        var service = Create(() => FileHistoryStatus.NotSetUp, () => OneDriveStatus.NotInstalled, () => ["Dropbox"]);

        var result = await service.GetAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(FileHistoryStatus.NotSetUp, result.Value!.FileHistory);
        Assert.Equal(["Dropbox"], result.Value.OtherTools);
    }

    [Fact]
    public async Task OneSourceThrowing_IsToleratedAndLeftNull()
    {
        var service = Create(() => throw new IOException("locked"), () => OneDriveStatus.NotInstalled);

        var result = await service.GetAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.FileHistory);
        Assert.NotNull(result.Value.OneDrive);
    }

    [Fact]
    public async Task ToolDetectorThrowing_GivesNoTools()
    {
        var service = Create(
            () => FileHistoryStatus.NotSetUp, () => OneDriveStatus.NotInstalled,
            () => throw new UnauthorizedAccessException());

        var result = await service.GetAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!.OtherTools);
    }

    [Fact]
    public async Task BothMainSourcesThrowing_Fails()
    {
        var service = Create(
            () => throw new System.Security.SecurityException(), () => throw new InvalidOperationException());

        var result = await service.GetAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Cancelled_Throws()
    {
        var service = Create(() => FileHistoryStatus.NotSetUp, () => OneDriveStatus.NotInstalled);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(cts.Token));
    }
}
