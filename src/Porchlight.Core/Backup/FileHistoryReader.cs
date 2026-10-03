using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Porchlight.Core.Backup;

/// <inheritdoc cref="IFileHistoryReader"/>
public sealed partial class FileHistoryReader(ILogger<FileHistoryReader> logger) : IFileHistoryReader
{
    private const string ConfigFolderRelativePath = @"Microsoft\Windows\FileHistory\Configuration";
    private const string ConfigFilePattern = "Config*.xml";
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\FileHistory";
    private const string ProtectedUpToTimeValue = "ProtectedUpToTime";

    public FileHistoryStatus Read()
    {
        var (configured, enabled) = ReadConfig();
        if (!configured)
        {
            return FileHistoryStatus.NotSetUp;
        }

        return new FileHistoryStatus(true, enabled, ReadLastBackup());
    }

    private (bool Configured, bool Enabled) ReadConfig()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigFolderRelativePath);
        if (!Directory.Exists(folder))
        {
            return (false, false);
        }

        var configured = false;
        var enabled = false;
        foreach (var file in Directory.EnumerateFiles(folder, ConfigFilePattern))
        {
            string xml;
            try
            {
                xml = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // One locked or unreadable config must not hide the others.
                LogConfigUnreadable(ex);
                continue;
            }

            if (FileHistoryConfigParser.Parse(xml) is not { } parsed)
            {
                LogConfigNotXml();
                continue;
            }

            configured |= parsed.IsConfigured;
            enabled |= parsed.IsConfigured && parsed.IsEnabled;
        }

        return (configured, enabled);
    }

    private DateTimeOffset? ReadLastBackup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        if (key?.GetValue(ProtectedUpToTimeValue) is not long fileTime || fileTime <= 0)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(DateTime.FromFileTimeUtc(fileTime), TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            LogBadTimestamp(ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "A File History config file could not be read.")]
    private partial void LogConfigUnreadable(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "A File History config file is not valid XML; ignoring it.")]
    private partial void LogConfigNotXml();

    [LoggerMessage(Level = LogLevel.Debug, Message = "File History's ProtectedUpToTime is not a valid timestamp.")]
    private partial void LogBadTimestamp(Exception ex);
}
