namespace Porchlight.Core.Startup;

/// <summary>The only registry access the Startup feature has: read <c>Run</c> values and read/write
/// one <c>StartupApproved</c> value. There is deliberately no delete.</summary>
public interface IStartupRegistry
{
    /// <summary>String values of a <c>Run</c> source's key (empty for a folder source or a missing key).</summary>
    IReadOnlyList<StartupRunValue> ReadRunValues(StartupSource source);

    /// <summary>The effective <c>StartupApproved</c> bytes for one item, or null if none is recorded.</summary>
    byte[]? ReadApproval(StartupSource source, string itemName);

    /// <summary>Writes the <c>StartupApproved</c> value for one item.</summary>
    void WriteApproval(StartupSource source, string itemName, byte[] value);
}
