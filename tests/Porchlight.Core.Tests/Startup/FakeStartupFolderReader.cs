using Porchlight.Core.Startup;

namespace Porchlight.Core.Tests.Startup;

internal sealed class FakeStartupFolderReader : IStartupFolderReader
{
    public Dictionary<StartupSource, List<StartupFolderItem>> Items { get; } = [];

    public IReadOnlyList<StartupFolderItem> ReadFolder(StartupSource source) =>
        Items.TryGetValue(source, out var items) ? items : [];
}
