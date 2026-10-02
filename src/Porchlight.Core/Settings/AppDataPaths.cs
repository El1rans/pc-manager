using System.Text.Json;

namespace Porchlight.Core.Settings;

/// <summary>
/// The one place that decides where Porchlight keeps its per-user data (<c>settings.json</c>,
/// logs, custom animations, the fan-control marker). Everything that reads or writes there builds
/// its path from <see cref="Root"/> rather than calling
/// <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> itself.
///
/// Normally <see cref="Root"/> is <c>%APPDATA%\Porchlight</c>. In a DEBUG build only, it can be
/// redirected so demo/test runs (screenshots, visual checks) never touch the real folder:
/// <list type="bullet">
/// <item><c>PORCHLIGHT_DATA_DIR=&lt;absolute path&gt;</c> uses that folder as the root.</item>
/// <item><c>PORCHLIGHT_DEMO_DATA=1</c> without <c>PORCHLIGHT_DATA_DIR</c> uses
/// <c>%TEMP%\Porchlight-demo</c>, seeded with first-run already completed (see
/// <see cref="PrepareOverrideFolder"/>).</item>
/// </list>
/// A Release build ignores both variables. See CONTRIBUTING.md's "Screenshots" section.
/// </summary>
public static class AppDataPaths
{
    /// <summary>DEBUG-only: absolute path to use instead of <c>%APPDATA%\Porchlight</c>.</summary>
    public const string DataDirVariable = "PORCHLIGHT_DATA_DIR";

    /// <summary>DEBUG-only demo data switch - see <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
    public const string DemoDataVariable = "PORCHLIGHT_DEMO_DATA";

    /// <summary>Folder name under <c>%APPDATA%</c> (or <c>%TEMP%</c> for the demo default).</summary>
    internal const string FolderName = "Porchlight";

    internal const string DemoFolderName = "Porchlight-demo";

    internal const string SettingsFileName = "settings.json";

#if DEBUG
    private const bool HonoursOverrides = true;
#else
    private const bool HonoursOverrides = false;
#endif

    private static readonly JsonSerializerOptions SeedJsonOptions = new() { WriteIndented = true };

    private static readonly Resolution Resolved = Resolve(
        Environment.GetEnvironmentVariable,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Path.GetTempPath(),
        HonoursOverrides);

    /// <summary>The per-user data folder: <c>%APPDATA%\Porchlight</c> unless a DEBUG override is active.</summary>
    public static string Root => Resolved.Root;

    /// <summary>True when a DEBUG override moved <see cref="Root"/> away from <c>%APPDATA%\Porchlight</c>.
    /// Always false in a Release build.</summary>
    public static bool IsOverridden => Resolved.Kind != OverrideKind.None;

    /// <summary><c>&lt;Root&gt;\settings.json</c>.</summary>
    public static string SettingsFile => Path.Combine(Root, SettingsFileName);

    /// <summary><c>&lt;Root&gt;\logs</c>.</summary>
    public static string LogsDirectory => Path.Combine(Root, "logs");

    /// <summary>The <c>%APPDATA%</c> folder itself - only for <see cref="AppDataMigrator"/>'s
    /// pre-rebrand <c>PCManager</c> folder, which lives beside the real root.</summary>
    internal static string RealApplicationData { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>
    /// When an override is active, creates its folder and - for the demo default only - seeds a
    /// <c>settings.json</c> with first-run completed so the setup wizard does not appear. An
    /// existing <c>settings.json</c> is never overwritten. Does nothing without an override.
    /// Called once, early in startup.
    /// </summary>
    public static void PrepareOverrideFolder() => PrepareOverrideFolder(Resolved);

    internal static void PrepareOverrideFolder(Resolution resolution)
    {
        if (resolution.Kind == OverrideKind.None)
        {
            return;
        }

        Directory.CreateDirectory(resolution.Root);
        if (resolution.Kind != OverrideKind.DemoDefault)
        {
            return;
        }

        var settingsPath = Path.Combine(resolution.Root, SettingsFileName);
        if (File.Exists(settingsPath))
        {
            return;
        }

        var seed = new AppSettings();
        seed.Setup.FirstRunCompleted = true;
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(seed, SeedJsonOptions));
    }

    /// <summary>
    /// Pure resolution logic, separated from the real environment for tests.
    /// <paramref name="honourOverrides"/> is false in a Release build, so neither variable has any effect.
    /// </summary>
    /// <exception cref="InvalidOperationException"><c>PORCHLIGHT_DATA_DIR</c> is set but not an
    /// absolute path - fail loudly rather than silently falling back to the real folder.</exception>
    internal static Resolution Resolve(
        Func<string, string?> getEnvironmentVariable,
        string applicationData,
        string tempPath,
        bool honourOverrides)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        if (honourOverrides)
        {
            var dataDir = getEnvironmentVariable(DataDirVariable);
            if (!string.IsNullOrWhiteSpace(dataDir))
            {
                if (!Path.IsPathFullyQualified(dataDir))
                {
                    throw new InvalidOperationException(
                        $"{DataDirVariable} must be an absolute path (got '{dataDir}').");
                }

                return new Resolution(Path.GetFullPath(dataDir), OverrideKind.DataDir);
            }

            if (getEnvironmentVariable(DemoDataVariable) == "1")
            {
                return new Resolution(Path.Combine(tempPath, DemoFolderName), OverrideKind.DemoDefault);
            }
        }

        return new Resolution(Path.Combine(applicationData, FolderName), OverrideKind.None);
    }

    internal enum OverrideKind
    {
        None,
        DataDir,
        DemoDefault,
    }

    internal sealed record Resolution(string Root, OverrideKind Kind);
}
