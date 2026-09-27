using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.Core.Tests;

public sealed class AppDataMigratorTests : IDisposable
{
    private readonly string _root;
    private readonly string _newDirectory;
    private readonly string _legacyDirectory;

    public AppDataMigratorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PorchlightMigratorTests_" + Guid.NewGuid().ToString("N"));
        _newDirectory = Path.Combine(_root, "Porchlight");
        _legacyDirectory = Path.Combine(_root, "PCManager");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void FreshInstall_NeitherDirectoryExists_DoesNothing()
    {
        AppDataMigrator.MigrateIfNeeded(NullLogger.Instance, _newDirectory, _legacyDirectory);

        Assert.False(Directory.Exists(_newDirectory));
        Assert.False(Directory.Exists(_legacyDirectory));
    }

    [Fact]
    public void Migrate_LegacyDirectoryExists_CopiesSettingsAndFanControlMarker()
    {
        Directory.CreateDirectory(_legacyDirectory);
        File.WriteAllText(Path.Combine(_legacyDirectory, "settings.json"), """{"Setup":{"LaunchCount":7}}""");
        File.WriteAllText(Path.Combine(_legacyDirectory, "fancontrol.active"), "2024-01-01T00:00:00Z");

        AppDataMigrator.MigrateIfNeeded(NullLogger.Instance, _newDirectory, _legacyDirectory);

        Assert.True(Directory.Exists(_newDirectory));
        Assert.Equal(
            """{"Setup":{"LaunchCount":7}}""",
            File.ReadAllText(Path.Combine(_newDirectory, "settings.json")));
        Assert.True(File.Exists(Path.Combine(_newDirectory, "fancontrol.active")));

        // The legacy folder and its files are left in place - never deleted or modified.
        Assert.True(File.Exists(Path.Combine(_legacyDirectory, "settings.json")));
        Assert.True(File.Exists(Path.Combine(_legacyDirectory, "fancontrol.active")));
    }

    [Fact]
    public void Migrate_OnlySettingsPresentInLegacyFolder_DoesNotCreateMarker()
    {
        Directory.CreateDirectory(_legacyDirectory);
        File.WriteAllText(Path.Combine(_legacyDirectory, "settings.json"), "{}");

        AppDataMigrator.MigrateIfNeeded(NullLogger.Instance, _newDirectory, _legacyDirectory);

        Assert.True(File.Exists(Path.Combine(_newDirectory, "settings.json")));
        Assert.False(File.Exists(Path.Combine(_newDirectory, "fancontrol.active")));
    }

    [Fact]
    public void BothDirectoriesExist_NeverOverwritesTheNewLocation()
    {
        Directory.CreateDirectory(_legacyDirectory);
        File.WriteAllText(Path.Combine(_legacyDirectory, "settings.json"), """{"Setup":{"LaunchCount":999}}""");

        Directory.CreateDirectory(_newDirectory);
        File.WriteAllText(Path.Combine(_newDirectory, "settings.json"), """{"Setup":{"LaunchCount":1}}""");

        AppDataMigrator.MigrateIfNeeded(NullLogger.Instance, _newDirectory, _legacyDirectory);

        Assert.Equal(
            """{"Setup":{"LaunchCount":1}}""",
            File.ReadAllText(Path.Combine(_newDirectory, "settings.json")));
    }

    [Fact]
    public void Migrate_CorruptLegacySettingsFile_CopiedFile_IsThenHandledByTheNormalCorruptFilePath()
    {
        Directory.CreateDirectory(_legacyDirectory);
        File.WriteAllText(Path.Combine(_legacyDirectory, "settings.json"), "{ not valid json");

        AppDataMigrator.MigrateIfNeeded(NullLogger.Instance, _newDirectory, _legacyDirectory);

        var newSettingsPath = Path.Combine(_newDirectory, "settings.json");
        Assert.True(File.Exists(newSettingsPath));

        // SettingsStore's own corrupt-file handling takes over from here: it should back up the
        // corrupt file and fall back to defaults, exactly as it would for a corrupt file that was
        // never migrated.
        var store = new SettingsStore(NullLogger<SettingsStore>.Instance, newSettingsPath);
        Assert.NotNull(store.Current);
        Assert.False(store.Current.Setup.FirstRunCompleted);
        Assert.True(File.Exists(newSettingsPath + ".bak"));
    }

    [Fact]
    public void Migrate_OneFileLockedByAnotherProcess_LogsAndStillMigratesTheOther()
    {
        Directory.CreateDirectory(_legacyDirectory);
        var settingsPath = Path.Combine(_legacyDirectory, "settings.json");
        File.WriteAllText(settingsPath, "{}");
        File.WriteAllText(Path.Combine(_legacyDirectory, "fancontrol.active"), "2024-01-01T00:00:00Z");

        // Exclusively locks settings.json so File.Copy for it throws IOException, simulating
        // another process transiently holding the file - the fan-control marker copy must still
        // succeed, and MigrateIfNeeded itself must not throw.
        using (new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Record.Exception(() =>
                AppDataMigrator.MigrateIfNeeded(NullLogger.Instance, _newDirectory, _legacyDirectory));
            Assert.Null(exception);
        }

        Assert.False(File.Exists(Path.Combine(_newDirectory, "settings.json")));
        Assert.True(File.Exists(Path.Combine(_newDirectory, "fancontrol.active")));
    }
}
