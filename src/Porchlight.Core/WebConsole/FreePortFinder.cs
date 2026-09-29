namespace Porchlight.Core.WebConsole;

/// <summary>Finds the free port closest to a preferred one, for the web console's first start
/// (see <see cref="WebConsoleController.ApplySettings"/>).</summary>
public static class FreePortFinder
{
    /// <summary>How far either side of the preferred port to look before giving up.</summary>
    public const int MaxSearchDistance = 100;

    /// <summary>
    /// Returns <paramref name="preferred"/> if it is free, otherwise the nearest free allowed port,
    /// trying <c>preferred + 1</c>, <c>preferred - 1</c>, <c>preferred + 2</c>, ... up to
    /// <see cref="MaxSearchDistance"/> away (above before below, so the default 8765 moves to 8766
    /// first). Ports outside <see cref="WebConsoleOptions.IsAllowedPort"/> are never returned.
    /// Returns null if nothing in range is free.
    /// </summary>
    public static int? FindNearest(int preferred, Func<int, bool> isFree)
    {
        ArgumentNullException.ThrowIfNull(isFree);

        for (var distance = 0; distance <= MaxSearchDistance; distance++)
        {
            if (TryCandidate(preferred + distance, isFree))
            {
                return preferred + distance;
            }

            if (distance > 0 && TryCandidate(preferred - distance, isFree))
            {
                return preferred - distance;
            }
        }

        return null;
    }

    private static bool TryCandidate(int port, Func<int, bool> isFree) =>
        WebConsoleOptions.IsAllowedPort(port) && isFree(port);
}
