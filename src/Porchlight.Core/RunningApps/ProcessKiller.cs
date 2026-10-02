using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.RunningApps;

/// <inheritdoc cref="IProcessKiller"/>
public sealed partial class ProcessKiller(ILogger<ProcessKiller> logger) : IProcessKiller
{
    private const int AccessDeniedErrorCode = 5;

    public ProcessKillOutcome Kill(int pid, DateTime? expectedStartTime)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (expectedStartTime is not null && process.StartTime != expectedStartTime)
            {
                return ProcessKillOutcome.StartTimeMismatch;
            }

            process.Kill(entireProcessTree: false);
            return ProcessKillOutcome.Killed;
        }
        catch (ArgumentException)
        {
            // No process with this id any more.
            return ProcessKillOutcome.AlreadyExited;
        }
        catch (InvalidOperationException)
        {
            // Exited between the lookup and the kill.
            return ProcessKillOutcome.AlreadyExited;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == AccessDeniedErrorCode)
        {
            return ProcessKillOutcome.AccessDenied;
        }
        catch (Win32Exception ex)
        {
            LogKillFailed(ex, pid);
            return ProcessKillOutcome.Failed;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not end process {Pid}.")]
    private partial void LogKillFailed(Exception ex, int pid);
}
