using Porchlight.Core.Lighting.Effects;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class SettingsDeviceExclusionProviderTests
{
    private sealed class FakeSettingsStore : ISettingsStore
    {
        public AppSettings Current { get; } = new();

        public void Save()
        {
        }

        public void Update(Action<AppSettings> mutate) => mutate(Current);
    }

    [Fact]
    public void IsExcluded_DeviceNotInList_ReturnsFalse()
    {
        var store = new FakeSettingsStore();
        var provider = new SettingsDeviceExclusionProvider(store);

        Assert.False(provider.IsExcluded("Keyboard"));
    }

    [Fact]
    public void IsExcluded_DeviceInList_ReturnsTrue()
    {
        var store = new FakeSettingsStore();
        store.Current.Lighting.ExcludedDeviceNames.Add("Keyboard");
        var provider = new SettingsDeviceExclusionProvider(store);

        Assert.True(provider.IsExcluded("Keyboard"));
    }

    [Fact]
    public void IsExcluded_IsCaseInsensitive()
    {
        var store = new FakeSettingsStore();
        store.Current.Lighting.ExcludedDeviceNames.Add("Keyboard");
        var provider = new SettingsDeviceExclusionProvider(store);

        Assert.True(provider.IsExcluded("KEYBOARD"));
    }

    [Fact]
    public void IsExcluded_ReflectsLiveChangesToSettings()
    {
        var store = new FakeSettingsStore();
        var provider = new SettingsDeviceExclusionProvider(store);

        Assert.False(provider.IsExcluded("Keyboard"));

        store.Update(s => s.Lighting.ExcludedDeviceNames.Add("Keyboard"));

        Assert.True(provider.IsExcluded("Keyboard"));
    }
}
