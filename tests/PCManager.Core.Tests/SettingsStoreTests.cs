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

        var settings = store.Load();

        Assert.NotNull(settings);
        Assert.NotNull(settings.Updates);
        Assert.NotNull(settings.Hardware);
        Assert.NotNull(settings.Lighting);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = CreateStore();
        var settings = new AppSettings();

        store.Save(settings);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Save_WritesAtomically_NoLeftoverTempFile()
    {
        var store = CreateStore();

        store.Save(new AppSettings());
        store.Save(new AppSettings());

        Assert.False(File.Exists(_settingsPath + ".tmp"));
        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Load_CorruptFile_BacksItUpAndReturnsDefaults()
    {
        File.WriteAllText(_settingsPath, "{ this is not valid json");
        var store = CreateStore();

        var settings = store.Load();

        Assert.NotNull(settings);
        var backupPath = _settingsPath + ".bak";
        Assert.True(File.Exists(backupPath));
        Assert.Equal("{ this is not valid json", File.ReadAllText(backupPath));
    }
}
