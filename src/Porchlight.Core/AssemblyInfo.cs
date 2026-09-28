using System.Runtime.CompilerServices;

// Lets tests use an internal constructor overload (e.g. FanControlManager's short-watchdog-timeout
// test seam), or assert on an internal, non-testable-any-other-way implementation detail (e.g.
// ProcessRunner.BuildDetachedStartInfo, which builds a ProcessStartInfo without actually spawning a
// process), without making any of it public API.
[assembly: InternalsVisibleTo("Porchlight.Core.Tests")]
