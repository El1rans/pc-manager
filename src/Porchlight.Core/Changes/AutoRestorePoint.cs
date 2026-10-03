using Microsoft.Extensions.Logging;
using Porchlight.Core.Health;
using Porchlight.Core.Settings;

namespace Porchlight.Core.Changes;

/// <inheritdoc cref="IAutoRestorePoint"/>
public sealed partial class AutoRestorePoint : IAutoRestorePoint
{
    /// <summary>After one attempt, further requests in the same session are skipped for this long, so a
    /// run of quick changes doesn't ask Windows again and again.</summary>
    public static readonly TimeSpan AttemptCooldown = TimeSpan.FromMinutes(10);

    public const string CreatedNote = "A restore point was made first, in case you need to go back.";

    public const string ProtectionOffNote = "No restore point was made because System Protection is turned off.";

    private readonly ISettingsStore _settings;
    private readonly IRestorePointService _restorePoints;
    private readonly ILogger<AutoRestorePoint> _logger;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private DateTimeOffset? _lastAttempt;

    public AutoRestorePoint(ISettingsStore settings, IRestorePointService restorePoints, ILogger<AutoRestorePoint> logger)
        : this(settings, restorePoints, logger, TimeProvider.System)
    {
    }

    /// <summary>Test seam: own clock.</summary>
    public AutoRestorePoint(
        ISettingsStore settings, IRestorePointService restorePoints, ILogger<AutoRestorePoint> logger, TimeProvider time)
    {
        _settings = settings;
        _restorePoints = restorePoints;
        _logger = logger;
        _time = time;
    }

    public async Task<AutoRestorePointResult> EnsureAsync(string reason, CancellationToken cancellationToken)
    {
        if (!_settings.Current.Changes.CreateRestorePointBeforeBigChanges)
        {
            return new AutoRestorePointResult(AutoRestorePointOutcome.SkippedSettingOff, null);
        }

        var now = _time.GetUtcNow();
        lock (_lock)
        {
            if (_lastAttempt is { } last && now - last < AttemptCooldown)
            {
                return new AutoRestorePointResult(AutoRestorePointOutcome.SkippedRecentAttempt, null);
            }

            _lastAttempt = now;
        }

        try
        {
            var status = await _restorePoints.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            if (!status.Succeeded || status.Value is null)
            {
                LogSkipped(status.Error ?? "status unreadable");
                return new AutoRestorePointResult(AutoRestorePointOutcome.SkippedUnavailable, null);
            }

            var availability = RestorePointRules.Evaluate(status.Value, now);
            if (!availability.CanCreate)
            {
                LogSkipped(availability.Message);
                return new AutoRestorePointResult(
                    AutoRestorePointOutcome.SkippedUnavailable,
                    status.Value.ProtectionEnabled == false ? ProtectionOffNote : null);
            }

            var result = await _restorePoints
                .CreateAsync(reason, RestorePointKind.ModifySettings, cancellationToken).ConfigureAwait(false);
            if (result.Outcome == RestorePointCreateOutcome.Created)
            {
                return new AutoRestorePointResult(AutoRestorePointOutcome.Created, CreatedNote);
            }

            LogSkipped(result.Message);
            return new AutoRestorePointResult(
                result.Outcome == RestorePointCreateOutcome.Failed
                    ? AutoRestorePointOutcome.Failed
                    : AutoRestorePointOutcome.SkippedUnavailable,
                result.Outcome == RestorePointCreateOutcome.ProtectionOff ? ProtectionOffNote : null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(ex);
            return new AutoRestorePointResult(AutoRestorePointOutcome.Failed, null);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "No automatic restore point was made: {Reason}")]
    private partial void LogSkipped(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Making an automatic restore point failed; carrying on without it.")]
    private partial void LogFailed(Exception ex);
}
