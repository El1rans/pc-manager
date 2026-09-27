namespace PCManager.Core.Monitoring;

/// <summary>Tells whether Windows has a pending restart (from servicing or Windows Update).</summary>
public interface IRestartDetector
{
    bool IsRestartPending();
}
