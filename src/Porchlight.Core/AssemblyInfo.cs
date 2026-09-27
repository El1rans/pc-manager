using System.Runtime.CompilerServices;

// Lets tests use an internal constructor overload (e.g. FanControlManager's short-watchdog-timeout
// test seam) without making it public API.
[assembly: InternalsVisibleTo("Porchlight.Core.Tests")]
