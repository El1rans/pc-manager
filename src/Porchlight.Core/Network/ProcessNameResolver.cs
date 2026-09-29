using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Network;

/// <inheritdoc cref="IProcessNameResolver"/>
public sealed class ProcessNameResolver : IProcessNameResolver
{
    /// <summary>Process ids 0 and 4 are Windows itself.</summary>
    private const int SystemPidLimit = 4;

    private const string SystemName = "Windows (system)";

    private readonly Dictionary<string, string> _friendlyByProcessName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private readonly ILogger<ProcessNameResolver> _logger;

    public ProcessNameResolver(ILogger<ProcessNameResolver> logger)
    {
        _logger = logger;
    }

    public string? GetFriendlyName(int pid)
    {
        if (pid is >= 0 and <= SystemPidLimit)
        {
            return SystemName;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            var name = process.ProcessName;
            lock (_lock)
            {
                if (_friendlyByProcessName.TryGetValue(name, out var cached))
                {
                    return cached;
                }
            }

            var friendly = ReadDescription(process) ?? name;
            lock (_lock)
            {
                _friendlyByProcessName[name] = friendly;
            }

            return friendly;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // The process ended between listing and lookup, or is protected. It is skipped.
            if (_logger.IsEnabled(LogLevel.Debug)) { _logger.LogDebug(ex, "Could not read the name of process {Pid}.", pid); }
            return null;
        }
    }

    private string? ReadDescription(Process process)
    {
        try
        {
            var description = process.MainModule?.FileVersionInfo.FileDescription;
            return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // Protected process: fall back to the plain process name.
            if (_logger.IsEnabled(LogLevel.Debug)) { _logger.LogDebug(ex, "Could not read the description of process {Name}.", process.ProcessName); }
            return null;
        }
    }
}
