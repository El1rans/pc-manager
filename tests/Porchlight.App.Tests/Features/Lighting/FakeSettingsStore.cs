using Porchlight.Core.Settings;

namespace Porchlight.App.Tests.Features.Lighting;

/// <summary>In-memory <see cref="ISettingsStore"/> fake for view model tests that only need to
/// prove a setting was written through <see cref="Update"/>, not real persistence to disk (see
/// <c>Porchlight.Core.Tests.SettingsStoreTests</c> for that).</summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    public AppSettings Current { get; } = new();

    public int UpdateCallCount { get; private set; }

    public void Save() { }

    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        UpdateCallCount++;
    }
}
