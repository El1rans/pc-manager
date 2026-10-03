using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Porchlight.Core.Settings;
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
    // A DEBUG demo/test run (AppDataPaths.IsOverridden, always false in Release) uses its own names,
    // so it starts alongside a real, running Porchlight instead of just activating it - see
    // CONTRIBUTING.md's "Isolated data folder" section.
    private static readonly ObjectNames Names =
        GetObjectNames(AppDataPaths.IsOverridden ? AppDataPaths.Root : null);

    /// <summary>The named mutex and activation event a run uses.</summary>
    public sealed record ObjectNames(string Mutex, string ActivateEvent);

    /// <summary>
    /// Pure name derivation, separated for tests. <paramref name="overriddenRoot"/> is null for a
    /// normal run, which gets the fixed release names. A DEBUG override run gets names suffixed with
    /// a short hash of its data folder (case-insensitive, trailing separators ignored), so runs
    /// against the same folder are still single-instance but different folders run side by side.
    /// </summary>
    public static ObjectNames GetObjectNames(string? overriddenRoot)
    {
        if (overriddenRoot is null)
        {
            return new ObjectNames("Local\\PorchlightSingleInstance", "Local\\PorchlightActivate");
        }

        var key = overriddenRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0, 6);
        return new ObjectNames(
            $"Local\\PorchlightSingleInstance-dev-{hash}", $"Local\\PorchlightActivate-dev-{hash}");
    }

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
            mutex = new Mutex(initiallyOwned: false, Names.Mutex);
            activate = new EventWaitHandle(false, EventResetMode.AutoReset, Names.ActivateEvent);

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
