using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Startup;

/// <inheritdoc cref="ILogonTaskSource"/>
public sealed partial class LogonTaskSource : ILogonTaskSource
{
    private const string SchedulerProgId = "Schedule.Service";
    private const string RootFolder = @"\";
    private const string WindowsOwnFolder = @"\Microsoft\Windows";
    private const int TaskEnumHidden = 1;          // TASK_ENUM_HIDDEN
    private const int TriggerTypeLogon = 9;        // TASK_TRIGGER_LOGON
    private const int ActionTypeExec = 0;          // TASK_ACTION_EXEC
    private const int RunLevelHighest = 1;         // TASK_RUNLEVEL_HIGHEST
    private const int AccessDeniedHResult = unchecked((int)0x80070005);

    private readonly ILogger<LogonTaskSource> _logger;

    public LogonTaskSource(ILogger<LogonTaskSource> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<LogonTask> ReadLogonTasks()
    {
        var tasks = new List<LogonTask>();
        object? service = null;
        try
        {
            service = Connect();
            dynamic root = ((dynamic)service).GetFolder(RootFolder);
            try
            {
                CollectFolder(root, tasks);
            }
            finally
            {
                Release(root);
            }
        }
        catch (COMException ex)
        {
            throw Translate(ex);
        }
        finally
        {
            Release(service);
        }

        return tasks;
    }

    public void SetEnabled(string taskPath, bool enabled)
    {
        object? service = null;
        object? task = null;
        try
        {
            service = Connect();
            task = ((dynamic)service).GetTask(taskPath);

            // The only write: the task's own Enabled flag (like the checkbox in Task Scheduler).
            ((dynamic)task).Enabled = enabled;
        }
        catch (COMException ex)
        {
            throw Translate(ex);
        }
        finally
        {
            Release(task);
            Release(service);
        }
    }

    private static object Connect()
    {
        var type = Type.GetTypeFromProgID(SchedulerProgId)
            ?? throw new IOException("The Windows Task Scheduler isn't available.");
        var service = Activator.CreateInstance(type)
            ?? throw new IOException("The Windows Task Scheduler isn't available.");
        ((dynamic)service).Connect();
        return service;
    }

    private void CollectFolder(dynamic folder, List<LogonTask> tasks)
    {
        string folderPath = folder.Path;
        if (IsWindowsOwn(folderPath))
        {
            return;
        }

        dynamic registered = folder.GetTasks(TaskEnumHidden);
        try
        {
            int count = registered.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic? task = null;
                try
                {
                    task = registered[i];
                    if (TryRead(task, out LogonTask? logonTask))
                    {
                        tasks.Add(logonTask!);
                    }
                }
                catch (Exception ex) when (IsComFailure(ex))
                {
                    LogTaskSkipped(ex, folderPath);
                }
                finally
                {
                    Release(task);
                }
            }
        }
        finally
        {
            Release(registered);
        }

        dynamic subFolders = folder.GetFolders(0);
        try
        {
            int count = subFolders.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic sub = subFolders[i];
                try
                {
                    CollectFolder(sub, tasks);
                }
                finally
                {
                    Release(sub);
                }
            }
        }
        finally
        {
            Release(subFolders);
        }
    }

    private static bool TryRead(dynamic task, out LogonTask? result)
    {
        result = null;
        dynamic definition = task.Definition;
        try
        {
            if (!HasEnabledLogonTrigger(definition))
            {
                return false;
            }

            string? executable = FirstExecPath(definition);
            if (executable is null)
            {
                return false;
            }

            string path = task.Path;
            string name = task.Name;
            bool enabled = task.Enabled;
            result = new LogonTask(path, name, executable.Length == 0 ? null : executable, enabled, IsMachineWide(definition));
            return true;
        }
        finally
        {
            Release(definition);
        }
    }

    private static bool HasEnabledLogonTrigger(dynamic definition)
    {
        dynamic triggers = definition.Triggers;
        try
        {
            int count = triggers.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic trigger = triggers[i];
                try
                {
                    int type = trigger.Type;
                    bool enabled = trigger.Enabled;
                    if (type == TriggerTypeLogon && enabled)
                    {
                        return true;
                    }
                }
                finally
                {
                    Release(trigger);
                }
            }

            return false;
        }
        finally
        {
            Release(triggers);
        }
    }

    /// <summary>Path of the first exec action ("" when it has no path); null when there is no exec
    /// action (COM-handler actions are not listed).</summary>
    private static string? FirstExecPath(dynamic definition)
    {
        dynamic actions = definition.Actions;
        try
        {
            int count = actions.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic action = actions[i];
                try
                {
                    int type = action.Type;
                    if (type == ActionTypeExec)
                    {
                        string? path = action.Path;
                        return string.IsNullOrWhiteSpace(path)
                            ? string.Empty
                            : Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
                    }
                }
                finally
                {
                    Release(action);
                }
            }

            return null;
        }
        finally
        {
            Release(actions);
        }
    }

    private static bool IsMachineWide(dynamic definition)
    {
        dynamic principal = definition.Principal;
        try
        {
            int runLevel = principal.RunLevel;
            string? userId = principal.UserId;
            return runLevel == RunLevelHighest || !IsCurrentUser(userId);
        }
        finally
        {
            Release(principal);
        }
    }

    private static bool IsCurrentUser(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return true;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var current = identity.Name;
        var bare = current[(current.LastIndexOf('\\') + 1)..];
        return userId.Equals(current, StringComparison.OrdinalIgnoreCase) ||
               userId.Equals(bare, StringComparison.OrdinalIgnoreCase) ||
               userId.Equals(identity.User?.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True for the Windows-owned <c>\Microsoft\Windows</c> tree.</summary>
    internal static bool IsWindowsOwn(string folderPath) =>
        folderPath.Equals(WindowsOwnFolder, StringComparison.OrdinalIgnoreCase) ||
        folderPath.StartsWith(WindowsOwnFolder + @"\", StringComparison.OrdinalIgnoreCase);

    private static bool IsComFailure(Exception ex) =>
        ex is COMException or InvalidCastException or MissingMethodException
            or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or UnauthorizedAccessException;

    private static Exception Translate(COMException ex) =>
        ex.HResult == AccessDeniedHResult
            ? new UnauthorizedAccessException("Access to the scheduled task was denied.", ex)
            : new IOException("The Windows Task Scheduler couldn't be read.", ex);

    private static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.FinalReleaseComObject(comObject);
        }
    }

    // Source-generated so nothing is formatted when Debug is off (CA1873).
    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read a scheduled task in {Folder}; skipping it.")]
    private partial void LogTaskSkipped(Exception ex, string folder);
}
