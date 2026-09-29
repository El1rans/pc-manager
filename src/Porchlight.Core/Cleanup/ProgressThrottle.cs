namespace Porchlight.Core.Cleanup;

/// <summary>Lets progress be reported at most once per interval (spec: every 100 ms), so a walk over
/// hundreds of thousands of files does not flood the UI thread.</summary>
internal sealed class ProgressThrottle
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _interval;
    private long _lastReportTimestamp;
    private bool _hasReported;

    public ProgressThrottle(TimeProvider timeProvider, TimeSpan interval)
    {
        _timeProvider = timeProvider;
        _interval = interval;
    }

    /// <summary>True if enough time has passed since the last accepted report.</summary>
    public bool ShouldReport()
    {
        var now = _timeProvider.GetTimestamp();
        if (_hasReported && _timeProvider.GetElapsedTime(_lastReportTimestamp, now) < _interval)
        {
            return false;
        }

        _hasReported = true;
        _lastReportTimestamp = now;
        return true;
    }
}
