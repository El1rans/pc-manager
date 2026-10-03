using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Porchlight.Core.Safety;

/// <inheritdoc cref="IWindowsUpdateAgent"/>
public sealed partial class WindowsUpdateAgent(ILogger<WindowsUpdateAgent> logger) : IWindowsUpdateAgent
{
    private const string SessionProgId = "Microsoft.Update.Session";
    private const string RebootRequiredKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired";
    private const string PendingCriteria = "IsInstalled=0 and IsHidden=0";

    /// <summary>How many history records to read (newest first); plenty for a 30 day window.</summary>
    private const int HistoryLimit = 300;

    /// <summary>IUpdateHistoryEntry.Operation value for an install (2 would be an uninstall).</summary>
    private const int OperationInstall = 1;

    public async Task<IReadOnlyList<UpdateHistoryEntry>?> ReadHistoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(ReadHistory, cancellationToken).WaitAsync(SafetyTimeouts.HistoryRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            LogFailed(ex, "update history");
            return null;
        }
    }

    public Task<bool?> IsRestartPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RebootRequiredKey);
            return Task.FromResult<bool?>(key is not null);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            LogFailed(ex, "restart-pending flag");
            return Task.FromResult<bool?>(null);
        }
    }

    public async Task<int?> CountPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(CountPending, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            LogFailed(ex, "pending updates search");
            return null;
        }
    }

    private static bool IsExpected(Exception ex) =>
        ex is COMException or InvalidOperationException or RuntimeBinderException or TimeoutException
            or UnauthorizedAccessException or InvalidCastException;

    private static dynamic CreateSession()
    {
        var type = Type.GetTypeFromProgID(SessionProgId)
            ?? throw new InvalidOperationException("The Windows Update Agent is not available.");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("The Windows Update Agent could not be started.");
    }

    private static List<UpdateHistoryEntry> ReadHistory()
    {
        dynamic session = CreateSession();
        dynamic searcher = session.CreateUpdateSearcher();
        try
        {
            int total = searcher.GetTotalHistoryCount();
            var count = Math.Min(total, HistoryLimit);
            var entries = new List<UpdateHistoryEntry>(count);
            if (count == 0)
            {
                return entries;
            }

            dynamic history = searcher.QueryHistory(0, count);
            for (var i = 0; i < history.Count; i++)
            {
                dynamic item = history[i];
                if ((int)item.Operation != OperationInstall)
                {
                    continue;
                }

                var date = new DateTimeOffset(DateTime.SpecifyKind((DateTime)item.Date, DateTimeKind.Utc));
                entries.Add(new UpdateHistoryEntry(date, (string)(item.Title ?? string.Empty), MapResult((int)item.ResultCode)));
            }

            return entries;
        }
        finally
        {
            Release(searcher);
            Release(session);
        }
    }

    private static int CountPending()
    {
        dynamic session = CreateSession();
        dynamic searcher = session.CreateUpdateSearcher();
        try
        {
            dynamic result = searcher.Search(PendingCriteria);
            return (int)result.Updates.Count;
        }
        finally
        {
            Release(searcher);
            Release(session);
        }
    }

    /// <summary>OperationResultCode: 0 not started, 1 in progress, 2 succeeded, 3 succeeded with errors,
    /// 4 failed, 5 aborted.</summary>
    private static UpdateHistoryResult MapResult(int code) => code switch
    {
        2 => UpdateHistoryResult.Succeeded,
        3 => UpdateHistoryResult.SucceededWithErrors,
        4 => UpdateHistoryResult.Failed,
        5 => UpdateHistoryResult.Aborted,
        _ => UpdateHistoryResult.InProgress,
    };

    private static void Release(object comObject)
    {
        if (Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Could not read {What} from Windows Update; showing 'couldn't check'.")]
    private partial void LogFailed(Exception ex, string what);
}
