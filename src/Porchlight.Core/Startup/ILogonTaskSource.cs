namespace Porchlight.Core.Startup;

/// <summary>Reads scheduled tasks that start at logon and flips their <c>Enabled</c> flag.</summary>
public interface ILogonTaskSource
{
    /// <summary>Tasks with an enabled logon trigger and an executable action, outside
    /// <c>\Microsoft\Windows\</c>. Throws <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/> when the scheduler can't be read.</summary>
    IReadOnlyList<LogonTask> ReadLogonTasks();

    /// <summary>Sets only the task's <c>Enabled</c> flag; never deletes, edits or runs the task.
    /// Throws <see cref="UnauthorizedAccessException"/> when access is denied.</summary>
    void SetEnabled(string taskPath, bool enabled);
}
