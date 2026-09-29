using Porchlight.Core.Startup;

namespace Porchlight.Core.Tests.Startup;

internal sealed class FakeFileProductInfoReader : IFileProductInfoReader
{
    public Dictionary<string, FileProductInfo> Infos { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Requested { get; } = [];

    public FileProductInfo? Read(string path)
    {
        Requested.Add(path);
        return Infos.GetValueOrDefault(path);
    }
}
