using Porchlight.Core.Settings;

namespace Porchlight.Core.Tests.WebConsole;

/// <summary>In-memory <see cref="ISettingsStore"/> for web console tests.</summary>
internal sealed class FakeWebConsoleSettingsStore : ISettingsStore
{
    public AppSettings Current { get; } = new();

    public void Save()
    {
    }

    public void Update(Action<AppSettings> mutate) => mutate(Current);
}
