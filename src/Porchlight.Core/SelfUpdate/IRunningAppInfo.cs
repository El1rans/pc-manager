using System.Reflection;

namespace Porchlight.Core.SelfUpdate;

/// <summary>What the self-updater needs to know about the running copy of Porchlight.</summary>
public interface IRunningAppInfo
{
    /// <summary>The running version (<c>Directory.Build.props</c> <c>Version</c>).</summary>
    Version Version { get; }

    /// <summary>The folder the running exe lives in, or null if it can't be determined.</summary>
    string? ExecutableDirectory { get; }
}

/// <inheritdoc cref="IRunningAppInfo"/>
public sealed class RunningAppInfo : IRunningAppInfo
{
    public Version Version { get; } = ReadVersion();

    public string? ExecutableDirectory { get; } = Path.GetDirectoryName(Environment.ProcessPath);

    private static Version ReadVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var informational = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (ReleaseParser.TryParseAppVersion(informational, out var parsed))
        {
            return parsed;
        }

        return assembly?.GetName().Version ?? new Version(0, 0, 0);
    }
}
