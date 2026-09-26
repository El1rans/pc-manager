using System.Diagnostics;

namespace PCManager.Core.Components;

/// <inheritdoc cref="IProcessProbe"/>
public sealed class ProcessProbe : IProcessProbe
{
    public bool IsRunning(string processName)
    {
        ArgumentException.ThrowIfNullOrEmpty(processName);

        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
