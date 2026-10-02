using System.Security;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Startup;

/// <inheritdoc cref="IStartupInfoReader"/>
public sealed partial class StartupInfoReader : IStartupInfoReader
{
    private readonly ILogger<StartupInfoReader> _logger;
    private readonly string _folder;
    private readonly string? _userSid;

    public StartupInfoReader(ILogger<StartupInfoReader> logger)
        : this(
            logger,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "wdi", "LogFiles", "StartupInfo"),
            CurrentUserSid())
    {
    }

    /// <summary>For tests: reads <paramref name="folder"/> for <paramref name="userSid"/>.</summary>
    internal StartupInfoReader(ILogger<StartupInfoReader> logger, string folder, string? userSid)
    {
        _logger = logger;
        _folder = folder;
        _userSid = userSid;
    }

    public StartupInfoReadResult Read()
    {
        if (string.IsNullOrEmpty(_userSid))
        {
            return StartupInfoReadResult.Empty;
        }

        try
        {
            if (!Directory.Exists(_folder))
            {
                return StartupInfoReadResult.Empty;
            }

            var newest = new DirectoryInfo(_folder)
                .EnumerateFiles($"{_userSid}_StartupInfo*.xml")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newest is null)
            {
                return StartupInfoReadResult.Empty;
            }

            using var stream = newest.OpenRead();
            return new StartupInfoReadResult(StartupInfoParser.Parse(stream), false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            LogAccessDenied(ex);
            return new StartupInfoReadResult([], true);
        }
        catch (Exception ex) when (ex is IOException)
        {
            LogUnreadable(ex);
            return StartupInfoReadResult.Empty;
        }
    }

    private static string? CurrentUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "The startup trace folder needs administrator rights; impact is not measured.")]
    private partial void LogAccessDenied(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read the startup trace; impact is not measured.")]
    private partial void LogUnreadable(Exception ex);
}
