using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Winget;

/// <summary>Reads an export file from disk with the size cap enforced before its contents are loaded.</summary>
public static class WingetExportFile
{
    /// <summary>Reads and validates the file at <paramref name="path"/>. An unreadable or oversized
    /// file is reported as a failed <see cref="WingetExportParseResult"/>, not an exception.</summary>
    public static async Task<WingetExportParseResult> LoadAsync(string path, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return WingetExportParseResult.Failure("Porchlight could not find that file.");
            }

            if (info.Length > WingetExportParser.MaxFileBytes)
            {
                return WingetExportParseResult.Failure("That file is too big to be an app list.");
            }

            var json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            return WingetExportParser.Parse(json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reported to the user as a plain message by the caller; logged here for diagnosis.
            logger.LogWarning(ex, "Could not read app list file.");
            return WingetExportParseResult.Failure("Porchlight could not open that file.");
        }
    }
}
