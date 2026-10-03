namespace Porchlight.Core.Changes;

/// <summary>What <see cref="IAutoRestorePoint.EnsureAsync"/> did. None of these block the change.</summary>
public enum AutoRestorePointOutcome
{
    Created,

    /// <summary>The "Create a restore point before big changes" setting is off.</summary>
    SkippedSettingOff,

    /// <summary>Windows won't make one now (System Protection off, frequency limit, status unreadable).</summary>
    SkippedUnavailable,

    /// <summary>One was already tried a moment ago in this session.</summary>
    SkippedRecentAttempt,

    /// <summary>Windows was asked and didn't make one.</summary>
    Failed,
}
