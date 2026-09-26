using Microsoft.Extensions.Logging;

namespace PCManager.Core.RemoteSupport;

/// <inheritdoc cref="IAnyDeskConfigReader"/>
public sealed partial class AnyDeskConfigReader : IAnyDeskConfigReader
{
    private readonly ILogger<AnyDeskConfigReader> _logger;

    public AnyDeskConfigReader(ILogger<AnyDeskConfigReader> logger)
    {
        _logger = logger;

        var anyDeskDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AnyDesk");
        SystemConfPath = Path.Combine(anyDeskDataDirectory, "system.conf");
        ServiceConfPath = Path.Combine(anyDeskDataDirectory, "service.conf");
    }

    public string SystemConfPath { get; }

    public string ServiceConfPath { get; }

    public string? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort fallback only - the CLI read is the primary path, so a config file we
            // cannot read just means we have nothing more to fall back to.
            LogCouldNotRead(path, ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read AnyDesk config file at {Path}.")]
    private partial void LogCouldNotRead(string path, Exception exception);
}
