using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Shell;
using Porchlight.App.Tray;
using Porchlight.Core.Alerts;

namespace Porchlight.App.Tests.Features.Notifications;

/// <summary>Tray icon that records the title of every balloon shown.</summary>
internal sealed class FakeTrayIcon : ITrayIcon
{
    public List<string> Balloons { get; } = [];

    public bool IsVisible => true;

    public event EventHandler? DoubleClicked
    {
        add { }
        remove { }
    }

    public void Show(IReadOnlyList<TrayMenuItem> menu) { }

    public void SetTooltip(string text) { }

    public void ShowBalloon(string title, string message, Action? onClick) => Balloons.Add(title);

    public void Dispose() { }
}

internal sealed class FakeShellWindowService : IShellWindowService
{
    public void ShowMainWindow() { }

    public void NavigateTo(Type pageViewModelType) { }

    public void RequestExit() { }
}

internal sealed class FakeAlertStateStore : IAlertStateStore
{
    private readonly Dictionary<string, DateTimeOffset> _fired = [];
    private DateTimeOffset? _restartSince;

    public DateTimeOffset? GetLastFired(string key) => _fired.TryGetValue(key, out var when) ? when : null;

    public void SetLastFired(string key, DateTimeOffset when) => _fired[key] = when;

    public DateTimeOffset? GetRestartPendingSince() => _restartSince;

    public void SetRestartPendingSince(DateTimeOffset? when) => _restartSince = when;
}

/// <summary>A <see cref="FakeTimeProvider"/> that counts the timers the code under test creates
/// (<c>Task.Delay</c> and <c>PeriodicTimer</c> both go through <see cref="CreateTimer"/>), so a test
/// can wait until a background loop has actually armed its next timer before moving time on -
/// otherwise an <c>Advance</c> that lands before the timer exists is silently lost.</summary>
internal sealed class TimerCountingTimeProvider(DateTimeOffset start) : FakeTimeProvider(start)
{
    private int _timersCreated;

    public int TimersCreated => Volatile.Read(ref _timersCreated);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        Interlocked.Increment(ref _timersCreated);
        return timer;
    }
}

/// <summary>Synchronisation for tests of hosted services whose loop runs on the thread pool.</summary>
internal static class BackgroundLoop
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>Polls until <paramref name="condition"/> holds or ten seconds pass. On timeout it
    /// just returns, so the assertion that follows reports what actually happened.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < WaitLimit)
        {
            await Task.Delay(5);
        }
    }

    /// <summary>Gives the loop a moment to run before asserting that something did NOT happen. Only
    /// used for negative checks: a slow machine can make such a check pass too easily, but never
    /// fail when the code is right.</summary>
    public static Task SettleAsync() => Task.Delay(50);
}
