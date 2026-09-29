namespace Porchlight.Core.Settings;

/// <summary>
/// Root of the application's persisted settings. Each feature owns one nested section so it can
/// evolve its own schema without touching the others.
/// </summary>
public sealed class AppSettings
{
    public UpdatesSettings Updates { get; set; } = new();

    public HardwareSettings Hardware { get; set; } = new();

    public LightingSettings Lighting { get; set; } = new();

    public RemoteSupportSettings RemoteSupport { get; set; } = new();

    public SetupSettings Setup { get; set; } = new();

    public CleanupSettings Cleanup { get; set; } = new();
    public NetworkSettings Network { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();

    public WebConsoleSettings WebConsole { get; set; } = new();

    public WindowSettings Window { get; set; } = new();
}
