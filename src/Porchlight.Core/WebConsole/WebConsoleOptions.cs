namespace Porchlight.Core.WebConsole;

/// <summary>Fixed limits and defaults for the read-only web console (see
/// docs/specs/12-web-console.md). Kept deliberately small: the console only ever serves a handful
/// of tiny GET requests to one person's browser.</summary>
public static class WebConsoleOptions
{
    /// <summary>Port used until the user picks another one.</summary>
    public const int DefaultPort = 8765;

    /// <summary>Lowest port the user may pick - below this are well-known ports that need admin
    /// rights or belong to other services.</summary>
    public const int LowestAllowedPort = 1024;

    /// <summary>Highest valid TCP port.</summary>
    public const int HighestAllowedPort = 65535;

    /// <summary>A request line plus headers larger than this is rejected. A browser's GET for this
    /// console is well under 2 KB.</summary>
    public const int MaxRequestHeadBytes = 8 * 1024;

    /// <summary>A request target (path plus query) longer than this is rejected.</summary>
    public const int MaxRequestTargetLength = 2048;

    /// <summary>Connections served at the same time; any beyond this are closed immediately, so a
    /// misbehaving client on the network cannot tie up the app.</summary>
    public const int MaxConcurrentConnections = 16;

    /// <summary>How long a client has to send its complete request before the connection is
    /// dropped.</summary>
    public static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Stats requests arriving closer together than this share one sample, so several open
    /// browser tabs (or a fast-polling client) never multiply the sampling work.</summary>
    public static readonly TimeSpan MinSampleInterval = TimeSpan.FromSeconds(1);

    /// <summary>If nobody has asked for stats for longer than this, rate-based numbers (CPU, disk,
    /// network) would be averaged over that whole idle gap - so the collector takes a throwaway
    /// sample first and waits <see cref="RePrimeDelay"/> before the real one.</summary>
    public static readonly TimeSpan StaleSampleThreshold = TimeSpan.FromSeconds(10);

    /// <summary>See <see cref="StaleSampleThreshold"/>.</summary>
    public static readonly TimeSpan RePrimeDelay = TimeSpan.FromSeconds(1);

    /// <summary>Drives change slowly; re-read them at most this often.</summary>
    public static readonly TimeSpan DriveRefreshInterval = TimeSpan.FromSeconds(15);

    /// <summary>A pending restart changes rarely; re-check at most this often.</summary>
    public static readonly TimeSpan RestartCheckInterval = TimeSpan.FromSeconds(60);

    /// <summary>How many process groups the console lists - the same as the dashboard.</summary>
    public const int TopProcessCount = 8;

    /// <summary>Whether <paramref name="port"/> is one the user may pick.</summary>
    public static bool IsAllowedPort(int port) => port is >= LowestAllowedPort and <= HighestAllowedPort;
}
