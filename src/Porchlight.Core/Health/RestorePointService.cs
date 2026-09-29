using System.Globalization;
using System.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Porchlight.Core.Health;

/// <inheritdoc cref="IRestorePointService"/>
public sealed partial class RestorePointService(ILogger<RestorePointService> logger) : IRestorePointService
{
    private const string DefaultScope = @"\\.\root\default";
    private const string SystemRestoreKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    private const string SystemRestorePolicyKey = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";
    private const string FrequencyValue = "SystemRestorePointCreationFrequency";
    private const string SessionIntervalValue = "RPSessionInterval";
    private const string DisableSrValue = "DisableSR";
    private const uint BeginSystemChange = 100;
    private const int RecentCount = 5;
    private const int ReturnOk = 0;

    public async Task<HealthReadResult<RestorePointStatus>> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(ReadStatus, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex) || ex is System.Security.SecurityException or IOException)
        {
            LogStatusFailed(ex);
            return HealthReadResult<RestorePointStatus>.Fail("Windows would not tell us about restore points.");
        }
    }

    public async Task<RestorePointCreateResult> CreateAsync(
        string description, RestorePointKind kind, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAt = DateTimeOffset.Now;
        try
        {
            var returnValue = await Task.Run(() => InvokeCreate(description, kind), cancellationToken)
                .ConfigureAwait(false);
            if (returnValue != ReturnOk)
            {
                LogCreateReturned(returnValue);
                var outcome = returnValue is RestorePointRules.ErrorServiceDisabled or RestorePointRules.ErrorServiceNotStarted
                    ? RestorePointCreateOutcome.ProtectionOff
                    : returnValue == RestorePointRules.ErrorAccessDenied ? RestorePointCreateOutcome.NeedsAdmin : RestorePointCreateOutcome.Failed;
                return new RestorePointCreateResult(outcome, RestorePointRules.DescribeError(returnValue));
            }

            // Windows returns success without creating anything when the frequency limit applies, so
            // check that a new restore point really appeared.
            var after = await Task.Run(ReadRecent, cancellationToken).ConfigureAwait(false);
            return RestorePointRules.WasCreated(after, startedAt)
                ? new RestorePointCreateResult(RestorePointCreateOutcome.Created, "Restore point created.")
                : new RestorePointCreateResult(
                    RestorePointCreateOutcome.NotCreatedTooSoon,
                    "Windows didn't make a new restore point because it made one recently. " +
                    "It only allows one every 24 hours by default.");
        }
        catch (UnauthorizedAccessException ex)
        {
            LogCreateFailed(ex);
            return new RestorePointCreateResult(
                RestorePointCreateOutcome.NeedsAdmin, RestorePointRules.DescribeError(RestorePointRules.ErrorAccessDenied));
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            LogCreateFailed(ex);
            return new RestorePointCreateResult(
                RestorePointCreateOutcome.Failed, RestorePointRules.DescribeError(-1));
        }
    }

    private HealthReadResult<RestorePointStatus> ReadStatus()
    {
        bool? enabled = null;
        var frequency = RestorePointRules.DefaultFrequencyMinutes;

        using (var policy = Registry.LocalMachine.OpenSubKey(SystemRestorePolicyKey))
        {
            if (policy?.GetValue(DisableSrValue) is int disableSr && disableSr == 1)
            {
                enabled = false;
            }
        }

        using (var key = Registry.LocalMachine.OpenSubKey(SystemRestoreKey))
        {
            if (key?.GetValue(FrequencyValue) is int configured && configured >= 0)
            {
                frequency = configured;
            }

            if (enabled is null && key?.GetValue(SessionIntervalValue) is int interval)
            {
                enabled = interval != 0;
            }
        }

        IReadOnlyList<RestorePointInfo>? recent;
        try
        {
            recent = ReadRecent();
        }
        catch (Exception ex) when (WmiReader.IsExpected(ex))
        {
            // The restore point list usually needs administrator rights; the rest of the card is
            // still useful without it.
            LogListFailed(ex);
            recent = null;
        }

        return HealthReadResult<RestorePointStatus>.Ok(new RestorePointStatus(enabled, frequency, recent));
    }

    private static List<RestorePointInfo> ReadRecent()
    {
        var rows = WmiReader.Query(
            @"root\default", "SELECT * FROM SystemRestore", ["SequenceNumber", "Description", "CreationTime"]);

        var points = new List<RestorePointInfo>(rows.Count);
        foreach (var row in rows)
        {
            var created = WmiReader.GetString(row, "CreationTime");
            if (created is null || WmiReader.GetInt(row, "SequenceNumber") is not { } sequence)
            {
                continue;
            }

            points.Add(new RestorePointInfo(
                sequence,
                WmiReader.GetString(row, "Description") ?? "Restore point",
                new DateTimeOffset(ManagementDateTimeConverter.ToDateTime(created))));
        }

        return points.OrderByDescending(p => p.CreatedAt).Take(RecentCount).ToList();
    }

    private static int InvokeCreate(string description, RestorePointKind kind)
    {
        var scope = new ManagementScope(DefaultScope, new ConnectionOptions { Timeout = HealthTimeouts.WmiQuery });
        using var systemRestore = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);
        using var inParams = systemRestore.GetMethodParameters("CreateRestorePoint");
        inParams["Description"] = description;
        inParams["RestorePointType"] = (uint)kind;
        inParams["EventType"] = BeginSystemChange;

        using var outParams = systemRestore.InvokeMethod(
            "CreateRestorePoint", inParams, new InvokeMethodOptions(null, HealthTimeouts.RestorePointCreate));
        return outParams?["ReturnValue"] is { } value
            ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
            : -1;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read System Restore status.")]
    private partial void LogStatusFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not list restore points (usually needs administrator rights).")]
    private partial void LogListFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CreateRestorePoint returned {ReturnValue}.")]
    private partial void LogCreateReturned(int returnValue);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Creating a restore point failed.")]
    private partial void LogCreateFailed(Exception ex);
}
