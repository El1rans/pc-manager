using Microsoft.Extensions.Logging;
using Porchlight.Core.Safety;
using Porchlight.Core.Startup;
using Porchlight.Core.Winget;

namespace Porchlight.Core.WebConsole;

/// <summary>
/// Builds the web console's extra views from services that already exist, reusing their logic:
/// the update list is whatever the Updates page last found (<see cref="IPendingUpdatesTracker"/> - the
/// console never runs <c>winget</c> itself, which takes seconds), startup items come from
/// <see cref="IStartupService"/> and the safety picture from <see cref="ISafetyStatusService"/>. Startup
/// and safety results are cached for <see cref="WebConsoleOptions.DetailsCacheTtl"/>, so browsers
/// polling every few seconds share one read. Everything here only reads.
/// </summary>
public sealed class WebConsoleDetailsCollector : IWebConsoleDetailsSource, IDisposable
{
    private const string UnavailableLine = "This could not be checked.";

    private readonly IPendingUpdatesTracker _updatesTracker;
    private readonly IStartupService _startupService;
    private readonly ISafetyStatusService _safetyService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebConsoleDetailsCollector> _logger;
    private readonly TtlCache<WebConsoleStartup> _startup;
    private readonly TtlCache<WebConsoleSecurity> _security;

    public WebConsoleDetailsCollector(
        IPendingUpdatesTracker updatesTracker,
        IStartupService startupService,
        ISafetyStatusService safetyService,
        ILogger<WebConsoleDetailsCollector> logger)
        : this(updatesTracker, startupService, safetyService, logger, TimeProvider.System)
    {
    }

    /// <summary>Test seam: a fake clock.</summary>
    internal WebConsoleDetailsCollector(
        IPendingUpdatesTracker updatesTracker,
        IStartupService startupService,
        ISafetyStatusService safetyService,
        ILogger<WebConsoleDetailsCollector> logger,
        TimeProvider timeProvider)
    {
        _updatesTracker = updatesTracker;
        _startupService = startupService;
        _safetyService = safetyService;
        _logger = logger;
        _timeProvider = timeProvider;
        _startup = new TtlCache<WebConsoleStartup>(timeProvider);
        _security = new TtlCache<WebConsoleSecurity>(timeProvider);
    }

    public void Dispose()
    {
        _startup.Dispose();
        _security.Dispose();
    }

    public Task<WebConsoleUpdates> GetUpdatesAsync(CancellationToken cancellationToken)
    {
        // Cheap (an in-memory read), so not cached.
        var result = _updatesTracker.Current is { } status
            ? new WebConsoleUpdates(
                true,
                status.CheckedAt,
                status.Count,
                status.Items.Select(u => new WebConsoleUpdateItem(u.Name, u.InstalledVersion, u.AvailableVersion)).ToList())
            : new WebConsoleUpdates(false, null, 0, []);
        return Task.FromResult(result);
    }

    public Task<WebConsoleStartup> GetStartupAsync(CancellationToken cancellationToken) =>
        _startup.GetAsync(async ct =>
        {
            try
            {
                var entries = await _startupService.ListAsync(ct).ConfigureAwait(false);
                var items = entries
                    .OrderByDescending(e => e.Impact)
                    .ThenByDescending(e => e.IsEnabled)
                    .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(e => new WebConsoleStartupItem(e.DisplayName, e.Publisher, e.IsEnabled, e.Impact))
                    .ToList();
                return new WebConsoleStartup(_startupService.ImpactNeedsAdmin, items);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web console could not list startup items.");
                return null;
            }
        }, new WebConsoleStartup(false, []), cancellationToken);

    public Task<WebConsoleSecurity> GetSecurityAsync(CancellationToken cancellationToken) =>
        _security.GetAsync(async ct =>
        {
            try
            {
                return Map(await _safetyService.GetAsync(ct).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web console could not read the safety status.");
                return null;
            }
        }, Unavailable(), cancellationToken);

    /// <summary>Turns the Safety page's model into the console's.</summary>
    internal static WebConsoleSecurity Map(SafetyStatus status)
    {
        var remoteLines = status.RemoteAccess.Tools
            .Select(t => t.SetUpByPorchlight
                ? $"{t.Name} - {t.StatusText} ({RemoteAccessStatus.PorchlightLabel})"
                : $"{t.Name} - {t.StatusText}")
            .ToList();
        return new WebConsoleSecurity(
            status.Headline,
            status.Level,
            new WebConsoleSecurityCard(status.Security.Verdict, status.Security.Level, status.Security.Details.ToList()),
            new WebConsoleSecurityCard(status.WindowsUpdate.Verdict, status.WindowsUpdate.Level, status.WindowsUpdate.Details),
            new WebConsoleSecurityCard(status.RemoteAccess.Verdict, status.RemoteAccess.Level, remoteLines));
    }

    private static WebConsoleSecurity Unavailable()
    {
        var card = new WebConsoleSecurityCard("Couldn't check", SafetyLevel.Unknown, [UnavailableLine]);
        return new WebConsoleSecurity("Couldn't check this PC", SafetyLevel.Unknown, card, card, card);
    }

    /// <summary>One value shared by every caller for <see cref="WebConsoleOptions.DetailsCacheTtl"/>.
    /// A failed read (null) serves the last good value, or <c>fallback</c> if there is none, and is
    /// retried on the next request.</summary>
    private sealed class TtlCache<T>(TimeProvider timeProvider) : IDisposable
        where T : class
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private T? _value;
        private DateTimeOffset _readAt;

        public void Dispose() => _gate.Dispose();

        public async Task<T> GetAsync(Func<CancellationToken, Task<T?>> read, T fallback, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var now = timeProvider.GetUtcNow();
                if (_value is not null && now - _readAt < WebConsoleOptions.DetailsCacheTtl)
                {
                    return _value;
                }

                var fresh = await read(cancellationToken).ConfigureAwait(false);
                if (fresh is not null)
                {
                    _value = fresh;
                    _readAt = timeProvider.GetUtcNow();
                    return fresh;
                }

                return _value ?? fallback;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
