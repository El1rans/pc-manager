using Porchlight.Core.Startup;

namespace Porchlight.Core.Tests.Startup;

/// <summary>In-memory <see cref="IStartupRegistry"/>: tests never touch the real registry. It has no
/// delete operation, mirroring the interface.</summary>
internal sealed class FakeStartupRegistry : IStartupRegistry
{
    public Dictionary<StartupSource, List<StartupRunValue>> RunValues { get; } = [];

    public Dictionary<(StartupSource Source, string Name), byte[]> Approvals { get; } = [];

    public List<(StartupSource Source, string Name, byte[] Value)> Writes { get; } = [];

    public Exception? ReadRunException { get; set; }

    public Exception? WriteException { get; set; }

    public IReadOnlyList<StartupRunValue> ReadRunValues(StartupSource source)
    {
        if (ReadRunException is not null)
        {
            throw ReadRunException;
        }

        return RunValues.TryGetValue(source, out var values) ? values : [];
    }

    public byte[]? ReadApproval(StartupSource source, string itemName) =>
        Approvals.GetValueOrDefault((source, itemName));

    public void WriteApproval(StartupSource source, string itemName, byte[] value)
    {
        if (WriteException is not null)
        {
            throw WriteException;
        }

        Writes.Add((source, itemName, value));
        Approvals[(source, itemName)] = value;
    }
}
