namespace Porchlight.Core.Safety;

/// <summary>Outcome of one Windows Update install attempt (Windows Update Agent <c>ResultCode</c>).</summary>
public enum UpdateHistoryResult
{
    Succeeded,
    SucceededWithErrors,
    Failed,
    Aborted,
    InProgress,
}
