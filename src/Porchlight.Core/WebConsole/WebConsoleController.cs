using Porchlight.Core.Settings;

namespace Porchlight.Core.WebConsole;

/// <summary>
/// Keeps <see cref="IWebConsoleServer"/> in line with <see cref="WebConsoleSettings"/>: turning the
/// console on or off, changing its port and replacing its access key all persist the setting and
/// then start, restart or stop the server to match. Used by the app at startup (to resume a
/// console left on) and by the Web console page.
/// </summary>
public sealed class WebConsoleController(
    IWebConsoleServer server,
    ISettingsStore settingsStore,
    IPortAvailability portAvailability)
{
    public IWebConsoleServer Server => server;

    public bool IsEnabled => settingsStore.Current.WebConsole.Enabled;

    public int ConfiguredPort => EffectivePort(settingsStore.Current.WebConsole.Port);

    /// <summary>The key a browser needs; empty until the console is first turned on.</summary>
    public string AccessKey => settingsStore.Current.WebConsole.AccessKey;

    /// <summary>Starts the server if the console is enabled in settings (creating an access key if
    /// there is none yet), or stops it if not. On the console's very first start (no access key yet),
    /// a busy port the user never picked is swapped for the nearest free one and saved, so the
    /// address stays the same from then on.</summary>
    public void ApplySettings()
    {
        if (!IsEnabled)
        {
            server.Stop();
            return;
        }

        if (string.IsNullOrEmpty(AccessKey))
        {
            MoveOffBusyDefaultPort();
            settingsStore.Update(s => s.WebConsole.AccessKey = AccessKeyGenerator.Generate());
        }

        server.Start(ConfiguredPort, AccessKey);
    }

    public void SetEnabled(bool enabled)
    {
        settingsStore.Update(s => s.WebConsole.Enabled = enabled);
        ApplySettings();
    }

    /// <summary>Saves <paramref name="port"/> and, if the console is on, restarts it there.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not one
    /// <see cref="WebConsoleOptions.IsAllowedPort"/> accepts.</exception>
    public void SetPort(int port)
    {
        if (!WebConsoleOptions.IsAllowedPort(port))
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port is outside the allowed range.");
        }

        settingsStore.Update(s =>
        {
            s.WebConsole.Port = port;
            s.WebConsole.PortChosenByUser = true;
        });
        if (IsEnabled)
        {
            ApplySettings();
        }
    }

    /// <summary>Replaces the access key, so every browser that had the old one loses access, and
    /// restarts the console (if on) to require the new one.</summary>
    public void RegenerateAccessKey()
    {
        settingsStore.Update(s => s.WebConsole.AccessKey = AccessKeyGenerator.Generate());
        if (IsEnabled)
        {
            ApplySettings();
        }
    }

    /// <summary>First start only: if the (not user-chosen) port is taken, use the closest free one.
    /// If nothing nearby is free, the port is left alone and the server reports its usual "port in
    /// use" failure.</summary>
    private void MoveOffBusyDefaultPort()
    {
        if (settingsStore.Current.WebConsole.PortChosenByUser)
        {
            return;
        }

        var preferred = ConfiguredPort;
        if (FreePortFinder.FindNearest(preferred, portAvailability.IsFree) is { } free && free != preferred)
        {
            settingsStore.Update(s => s.WebConsole.Port = free);
        }
    }

    /// <summary>A hand-edited settings file with an out-of-range port falls back to the default
    /// rather than failing.</summary>
    private static int EffectivePort(int port) =>
        WebConsoleOptions.IsAllowedPort(port) ? port : WebConsoleOptions.DefaultPort;
}
