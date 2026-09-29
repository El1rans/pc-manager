using System.Diagnostics.Eventing.Reader;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Health;

/// <inheritdoc cref="IProblemEventReader"/>
public sealed partial class EventLogProblemReader(ILogger<EventLogProblemReader> logger) : IProblemEventReader
{
    private const int MaxRecordsPerLog = 5000;
    private const double MillisecondsPerDay = 24 * 60 * 60 * 1000;

    private const string ApplicationFilter = "(EventID=1000 or EventID=1001)";
    private const string SystemFilter =
        "(EventID=41 or EventID=6008 or EventID=1001 or EventID=7 or EventID=51 or EventID=153 or EventID=55 or EventID=20)";

    public async Task<HealthReadResult<IReadOnlyList<HealthEventRecord>>> ReadAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HealthTimeouts.EventLogRead);
        try
        {
            var records = await Task.Run(() => ReadAll(timeout.Token), timeout.Token).ConfigureAwait(false);
            return HealthReadResult<IReadOnlyList<HealthEventRecord>>.Ok(records);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut();
            return HealthReadResult<IReadOnlyList<HealthEventRecord>>.Fail("Reading the Windows event log took too long.");
        }
        catch (Exception ex) when (ex is EventLogException or UnauthorizedAccessException or InvalidOperationException)
        {
            LogReadFailed(ex);
            return HealthReadResult<IReadOnlyList<HealthEventRecord>>.Fail("Windows would not let us read its event log.");
        }
    }

    private static List<HealthEventRecord> ReadAll(CancellationToken token)
    {
        var records = new List<HealthEventRecord>();
        ReadLog("Application", ApplicationFilter, records, token);
        ReadLog("System", SystemFilter, records, token);
        return records;
    }

    private static void ReadLog(string logName, string idFilter, List<HealthEventRecord> into, CancellationToken token)
    {
        var windowMs = ProblemSummarizer.WindowDays * MillisecondsPerDay;
        var xpath = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"*[System[{idFilter} and TimeCreated[timediff(@SystemTime) <= {windowMs:0}]]]");

        using var reader = new EventLogReader(new EventLogQuery(logName, PathType.LogName, xpath));
        for (var i = 0; i < MaxRecordsPerLog; i++)
        {
            token.ThrowIfCancellationRequested();
            using var record = reader.ReadEvent();
            if (record is null)
            {
                return;
            }

            if (record.TimeCreated is not { } created)
            {
                continue;
            }

            var props = record.Properties.Select(p => p.Value?.ToString() ?? string.Empty).ToList();
            into.Add(new HealthEventRecord(record.ProviderName ?? string.Empty, record.Id, created, props));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the Windows event log.")]
    private partial void LogReadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the Windows event log timed out.")]
    private partial void LogTimedOut();
}
