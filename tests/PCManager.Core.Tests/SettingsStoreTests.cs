using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using PCManager.Core.Settings;
using Xunit;

namespace PCManager.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _settingsPath;

    public SettingsStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PCManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _settingsPath = Path.Combine(_directory, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SettingsStore CreateStore() => new(NullLogger<SettingsStore>.Instance, _settingsPath);

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var store = CreateStore();

        Assert.NotNull(store.Current);
        Assert.NotNull(store.Current.Updates);
        Assert.NotNull(store.Current.Hardware);
        Assert.NotNull(store.Current.Lighting);
        Assert.NotNull(store.Current.RemoteSupport);
        Assert.NotNull(store.Current.Setup);
        Assert.False(store.Current.Setup.FirstRunCompleted);
    }

    [Fact]
    public void Update_ThenReloadFromDisk_PersistsTheChange()
    {
        var store = CreateStore();

        store.Update(s => s.Setup.FirstRunCompleted = true);

        var reloaded = CreateStore();
        Assert.True(reloaded.Current.Setup.FirstRunCompleted);
    }

    [Fact]
    public void Save_WritesAtomically_NoLeftoverTempFile()
    {
        var store = CreateStore();

        store.Save();
        store.Update(s => s.Setup.LaunchCount++);

        Assert.False(Directory.EnumerateFiles(_directory, "*.tmp").Any());
        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Save_WhenTargetFileIsMissing_CreatesIt()
    {
        var store = CreateStore();
        Assert.False(File.Exists(_settingsPath));

        store.Save();

        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Load_CorruptFile_BacksItUpAndReturnsDefaults()
    {
        File.WriteAllText(_settingsPath, "{ this is not valid json");

        var store = CreateStore();

        Assert.NotNull(store.Current);
        var backupPath = _settingsPath + ".bak";
        Assert.True(File.Exists(backupPath));
        Assert.Equal("{ this is not valid json", File.ReadAllText(backupPath));
    }

    [Fact]
    public void Load_UnknownFields_AreIgnored()
    {
        File.WriteAllText(_settingsPath, """{"SomethingFromTheFuture": 42, "Setup": {"FirstRunCompleted": true}}""");

        var store = CreateStore();

        Assert.True(store.Current.Setup.FirstRunCompleted);
    }

    [Fact]
    public void Load_NullSection_FallsBackToDefaultForThatSection()
    {
        File.WriteAllText(_settingsPath, """{"Updates": null}""");

        var store = CreateStore();

        Assert.NotNull(store.Current.Updates);
    }

    [Fact]
    public void Save_WithStaleUnrelatedTempFilePresent_StillSucceeds()
    {
        File.WriteAllText(Path.Combine(_directory, "settings.json.stale-leftover.tmp"), "garbage from a crashed run");
        var store = CreateStore();

        store.Update(s => s.Setup.FirstRunCompleted = true);

        Assert.True(File.Exists(_settingsPath));
        var reloaded = CreateStore();
        Assert.True(reloaded.Current.Setup.FirstRunCompleted);
    }

    [Fact]
    public void Update_WhenTargetFileTransientlyLocked_RetriesAndPersists()
    {
        var store = CreateStore();
        store.Save();

        using var lockingStream = new FileStream(
            _settingsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        using var releaseTimer = new Timer(
            _ => lockingStream.Dispose(),
            state: null,
            dueTime: TimeSpan.FromMilliseconds(50),
            period: Timeout.InfiniteTimeSpan);

        store.Update(s => s.Setup.LaunchCount++);

        Assert.Equal(1, store.Current.Setup.LaunchCount);

        var reloaded = CreateStore();
        Assert.Equal(1, reloaded.Current.Setup.LaunchCount);
    }

    [Fact]
    public void Update_WhenTargetFilePermanentlyLocked_DoesNotThrow_AndCurrentReflectsChange()
    {
        var store = CreateStore();
        store.Save();

        using var lockingStream = new FileStream(
            _settingsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var exception = Record.Exception(() => store.Update(s => s.Setup.LaunchCount++));

        Assert.Null(exception);
        Assert.Equal(1, store.Current.Setup.LaunchCount);
    }

    [Fact]
    public void Update_FromManyThreadsConcurrently_LosesNoIncrements()
    {
        var store = CreateStore();
        const int threadCount = 8;
        const int incrementsPerThread = 50;

        Parallel.For(0, threadCount, _ =>
        {
            for (var i = 0; i < incrementsPerThread; i++)
            {
                store.Update(s => s.Setup.LaunchCount++);
            }
        });

        Assert.Equal(threadCount * incrementsPerThread, store.Current.Setup.LaunchCount);

        var reloaded = CreateStore();
        Assert.Equal(threadCount * incrementsPerThread, reloaded.Current.Setup.LaunchCount);
    }
}
