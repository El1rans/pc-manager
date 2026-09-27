namespace PCManager.Core.Monitoring.Demo;

/// <summary>Gate for the DEBUG-only "demo data" mode: when enabled, the Dashboard's monitoring
/// services (<see cref="ISystemInfoProvider"/>, <see cref="IDriveMonitor"/>,
/// <see cref="IProcessMonitor"/>) are swapped for fake ones reporting made-up machine identity,
/// drives and processes, so screenshots for documentation never show the real machine's computer
/// name, hardware, drive labels or running/installed apps.
///
/// Enabled by setting the environment variable <c>PCMANAGER_DEMO_DATA=1</c> before launching a
/// DEBUG build of PC Manager (<c>dotnet run --project src/PCManager.App</c> - the default
/// configuration is Debug). Has no effect - and does not exist in the compiled output - in a
/// Release build: everything here is inside <c>#if DEBUG</c>, so this is not a shipping feature.
/// See CONTRIBUTING.md's "Screenshots" section for how to use this to (re)capture documentation
/// screenshots.</summary>
internal static class DemoDataMode
{
#if DEBUG
    public static bool IsEnabled { get; } =
        Environment.GetEnvironmentVariable("PCMANAGER_DEMO_DATA") == "1";
#else
    public const bool IsEnabled = false;
#endif
}
