using System.Security;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Health;

namespace Porchlight.Core.Backup;

/// <inheritdoc cref="IBackupStatusService"/>
public sealed partial class BackupStatusService(
    IFileHistoryReader fileHistory,
    IOneDriveReader oneDrive,
    IBackupToolDetector toolDetector,
    ILogger<BackupStatusService> logger) : IBackupStatusService
{
    public async Task<HealthReadResult<BackupSnapshot>> GetAsync(CancellationToken cancellationToken)
    {
        var history = await Task.Run(() => Tolerate(fileHistory.Read, "File History"), cancellationToken).ConfigureAwait(false);
        var drive = await Task.Run(() => Tolerate(oneDrive.Read, "OneDrive"), cancellationToken).ConfigureAwait(false);
        var tools = await Task.Run(() => Tolerate(toolDetector.Find, "backup tools"), cancellationToken).ConfigureAwait(false);

        if (history is null && drive is null)
        {
            return HealthReadResult<BackupSnapshot>.Fail("Windows would not tell us about backups.");
        }

        return HealthReadResult<BackupSnapshot>.Ok(new BackupSnapshot(history, drive, tools ?? []));
    }

    private T? Tolerate<T>(Func<T> read, string source)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
            or InvalidOperationException)
        {
            LogSourceFailed(ex, source);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read backup source {Source}.")]
    private partial void LogSourceFailed(Exception ex, string source);
}
