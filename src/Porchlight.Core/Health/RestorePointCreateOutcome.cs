namespace Porchlight.Core.Health;

/// <summary>Result of asking Windows for a restore point.</summary>
public enum RestorePointCreateOutcome
{
    Created,

    /// <summary>Windows said OK but skipped it because one was made recently (24 h default).</summary>
    NotCreatedTooSoon,

    /// <summary>System Protection is off.</summary>
    ProtectionOff,

    NeedsAdmin,

    Failed,
}
