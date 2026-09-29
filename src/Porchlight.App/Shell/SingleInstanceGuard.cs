using System.IO;
using System.Security;
using Serilog;

namespace Porchlight.App.Shell;

/// <summary>
/// Lets only one Porchlight run per Windows session. A second start signals the first (which shows
/// its window, possibly hidden in the tray) and exits. Ownership is waited on briefly so the
/// elevated relaunch, which starts the new process while the old one is still shutting down, still
/// works. If the named objects cannot be created or opened (for example the running instance has a
/// different elevation level) this fails open: the app just runs normally.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Local\\PorchlightSingleInstance";
    private const string ActivateEventName = "Local\\PorchlightActivate";

    /// <summary>How long a second instance waits for the first to finish exiting before assuming it
    /// is staying, signalling it and giving up.</summary>
    private static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(5);

    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _registration;

    private SingleInstanceGuard(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }

    /// <summary>False when another instance already owns the app; the caller should exit.</summary>
    public bool IsPrimary { get; }

    public static SingleInstanceGuard TryAcquire()
    {
        Mutex? mutex = null;
        EventWaitHandle? activate = null;
        try
        {
            mutex = new Mutex(initiallyOwned: false, MutexName);
            activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);

            bool owned;
            try
            {
                owned = mutex.WaitOne(AcquireTimeout);
            }
            catch (AbandonedMutexException)
            {
                // The previous instance died without releasing it; ownership is ours now.
                owned = true;
            }

            if (!owned)
            {
                activate.Set();
                mutex.Dispose();
                activate.Dispose();
                return new SingleInstanceGuard(isPrimary: false);
            }

            return new SingleInstanceGuard(isPrimary: true) { _mutex = mutex, _activateEvent = activate };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException or SecurityException)
        {
            Log.Warning(ex, "Single-instance protection is unavailable; running without it.");
            mutex?.Dispose();
            activate?.Dispose();
            return new SingleInstanceGuard(isPrimary: true);
        }
    }

    /// <summary>Calls <paramref name="onActivate"/> (on a thread-pool thread) each time another
    /// instance asks this one to show itself.</summary>
    public void StartListening(Action onActivate)
    {
        if (_activateEvent is null)
        {
            return;
        }

        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent, (_, _) => onActivate(), null, Timeout.InfiniteTimeSpan, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _registration = null;

        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException ex)
            {
                // Released from a thread that does not own it (never expected); the process is
                // exiting and the OS frees the handle regardless.
                Log.Debug(ex, "Single-instance mutex was not owned by the disposing thread.");
            }

            _mutex.Dispose();
            _mutex = null;
        }

        _activateEvent?.Dispose();
        _activateEvent = null;
    }
}
