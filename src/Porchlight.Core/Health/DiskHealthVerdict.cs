namespace Porchlight.Core.Health;

/// <summary>Plain overall verdict for one physical disk.</summary>
public enum DiskHealthVerdict
{
    /// <summary>Not enough information to say either way.</summary>
    Unknown,

    /// <summary>Nothing worrying reported.</summary>
    Healthy,

    /// <summary>Something suggests the drive may fail; back up files.</summary>
    Warning,
}
