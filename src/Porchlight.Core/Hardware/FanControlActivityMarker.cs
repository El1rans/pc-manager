using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Hardware;

/// <inheritdoc cref="IFanControlActivityMarker"/>
public sealed class FanControlActivityMarker : IFanControlActivityMarker
{
    private readonly ILogger<FanControlActivityMarker> _logger;
    private readonly string _path;

    public FanControlActivityMarker(ILogger<FanControlActivityMarker> logger)
        : this(logger, DefaultPath())
    {
    }

    /// <summary>Test seam: lets tests point this at a temp file.</summary>
    public FanControlActivityMarker(ILogger<FanControlActivityMarker> logger, string path)
    {
        _logger = logger;
        _path = path;
    }

    private static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Porchlight", "fancontrol.active");

    public bool Exists()
    {
        try
        {
            return File.Exists(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not check for the fan-control activity marker.");
            return false;
        }
    }

    public void Create()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: failing to write this marker must not stop fan control itself - it only
            // means a future launch cannot warn about an unclean shutdown.
            _logger.LogWarning(ex, "Could not write the fan-control activity marker.");
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not delete the fan-control activity marker.");
        }
    }
}
