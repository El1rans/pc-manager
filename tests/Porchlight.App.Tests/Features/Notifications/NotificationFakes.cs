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
