namespace Porchlight.Core.Startup;

/// <summary>Manages the "Start Porchlight when I sign in to Windows" scheduled task. A scheduled
/// task (not a Run key) is used because it can start Porchlight elevated with no UAC prompt at
/// sign-in. See <c>docs/specs/24-start-at-login.md</c>.</summary>
public interface ILoginLaunchService
{
    /// <summary>Reads the real task state from Task Scheduler.</summary>
    Task<LoginLaunchState> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>Registers (or replaces) the task for the current exe. Needs admin: asks for it once
    /// via UAC when Porchlight is not already elevated.</summary>
    Task<LoginLaunchResult> EnableAsync(CancellationToken cancellationToken);

    /// <summary>Removes the task; succeeds if it does not exist. Needs admin like
    /// <see cref="EnableAsync"/>.</summary>
    Task<LoginLaunchResult> DisableAsync(CancellationToken cancellationToken);

    /// <summary>If the task exists but points at a different exe than the running one, re-registers
    /// it - but only when already elevated (never prompts). Best-effort; returns true if it re-registered.</summary>
    Task<bool> RefreshStaleRegistrationAsync(CancellationToken cancellationToken);
}

/// <summary>State of the scheduled task. <paramref name="CommandPath"/> and <paramref name="Arguments"/>
/// are null when the task does not exist or could not be parsed.</summary>
public sealed record LoginLaunchState(bool Exists, string? CommandPath, string? Arguments);

/// <summary>How an enable/disable attempt ended.</summary>
public enum LoginLaunchOutcome
{
    Succeeded,

    /// <summary>The user declined the UAC prompt.</summary>
    Declined,

    Failed,
}

/// <summary>Result of <see cref="ILoginLaunchService.EnableAsync"/>/<see cref="ILoginLaunchService.DisableAsync"/>.
/// <paramref name="Message"/> is friendly text safe to show the user (null on success).</summary>
public sealed record LoginLaunchResult(LoginLaunchOutcome Outcome, string? Message)
{
    public static LoginLaunchResult Success { get; } = new(LoginLaunchOutcome.Succeeded, null);

    public bool Succeeded => Outcome == LoginLaunchOutcome.Succeeded;
}
