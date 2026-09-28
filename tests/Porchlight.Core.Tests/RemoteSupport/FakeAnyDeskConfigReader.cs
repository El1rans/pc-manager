using Porchlight.Core.RemoteSupport;

namespace Porchlight.Core.Tests.RemoteSupport;

internal sealed class FakeAnyDeskConfigReader : IAnyDeskConfigReader
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public string SystemConfPath { get; } = @"C:\ProgramData\AnyDesk\system.conf";

    public string ServiceConfPath { get; } = @"C:\ProgramData\AnyDesk\service.conf";

    public void SetFile(string path, string contents) => _files[path] = contents;

    public string? TryRead(string path) => _files.GetValueOrDefault(path);
}
