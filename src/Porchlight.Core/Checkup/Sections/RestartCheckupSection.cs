using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Checkup.Sections;

/// <summary>How long the PC has been on and whether Windows is waiting for a restart.</summary>
public sealed class RestartCheckupSection : ICheckupSection
{
    /// <summary>Days without a restart after which the report suggests one.</summary>
    public const int LongUptimeDays = 14;

    private readonly ISystemInfoProvider _systemInfo;
    private readonly IRestartDetector _restartDetector;
    private readonly TimeProvider _timeProvider;

    public RestartCheckupSection(ISystemInfoProvider systemInfo, IRestartDetector restartDetector, TimeProvider timeProvider)
    {
        _systemInfo = systemInfo;
        _restartDetector = restartDetector;
        _timeProvider = timeProvider;
    }

    public string Title => "Restarts";

    public int Order => 200;

    public async Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken)
    {
        var info = await _systemInfo.GetAsync(cancellationToken).ConfigureAwait(false);
        var severity = CheckupSeverity.Ok;
        List<string> lines = [];

        if (info.LastBootTimeUtc is { } boot)
        {
            var uptime = _timeProvider.GetUtcNow().UtcDateTime - boot;
            lines.Add($"This PC has been on for {ByteFormatter.FormatDuration(uptime)}.");
            if (uptime.TotalDays >= LongUptimeDays)
            {
                severity = CheckupSeverity.NeedsAttention;
                lines.Add("It has not been restarted for a while. A restart is a good idea.");
            }
        }
        else
        {
            lines.Add("Porchlight could not tell how long this PC has been on.");
        }

        if (_restartDetector.IsRestartPending())
        {
            severity = CheckupSeverity.NeedsAttention;
            lines.Add("Windows is waiting for a restart to finish installing updates.");
        }
        else
        {
            lines.Add("Windows is not waiting for a restart.");
        }

        return new CheckupSectionResult(Title, severity, lines);
    }
}
