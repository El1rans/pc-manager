using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Startup;

/// <inheritdoc cref="IFileProductInfoReader"/>
public sealed partial class FileProductInfoReader : IFileProductInfoReader
{
    private readonly ILogger<FileProductInfoReader> _logger;

    public FileProductInfoReader(ILogger<FileProductInfoReader> logger)
    {
        _logger = logger;
    }

    public FileProductInfo? Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            // Reads the version resource only; the file is never executed.
            var info = FileVersionInfo.GetVersionInfo(path);
            return new FileProductInfo(info.FileDescription, info.ProductName, info.CompanyName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            LogUnreadable(ex, path);
            return null;
        }
    }

    // Source-generated so nothing is formatted when Debug is off (CA1873).
    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read version info of {Path}; using the file name instead.")]
    private partial void LogUnreadable(Exception ex, string path);
}
