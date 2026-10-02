using Porchlight.Core.Startup;

namespace Porchlight.Core.Tests.Startup;

internal sealed class FakeLogonTaskSource : ILogonTaskSource
{
    public List<LogonTask> Tasks { get; } = [];

    public Exception? ReadException { get; set; }

    public Exception? SetException { get; set; }

    public List<(string Path, bool Enabled)> Changes { get; } = [];

    public IReadOnlyList<LogonTask> ReadLogonTasks() => ReadException is null ? [.. Tasks] : throw ReadException;

    public void SetEnabled(string taskPath, bool enabled)
    {
        if (SetException is not null)
        {
            throw SetException;
        }

        Changes.Add((taskPath, enabled));
    }
}
