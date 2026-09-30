namespace Porchlight.Core.SelfUpdate;

/// <summary>Decides whether the running copy is the installed one (and so may update itself).</summary>
public interface IInstallTypeDetector
{
    InstallType Detect();
}

/// <inheritdoc cref="IInstallTypeDetector"/>
public sealed class InstallTypeDetector : IInstallTypeDetector
{
    private readonly IRunningAppInfo _appInfo;
    private readonly IInstallLocationReader _locationReader;

    public InstallTypeDetector(IRunningAppInfo appInfo, IInstallLocationReader locationReader)
    {
        _appInfo = appInfo;
        _locationReader = locationReader;
    }

    /// <summary>"Installed" only when the running exe's folder equals the installer's recorded
    /// <c>InstallLocation</c> (case-insensitive, after normalising the paths); otherwise "portable".</summary>
    public InstallType Detect()
    {
        var installed = Normalize(_locationReader.GetInstallLocation());
        var running = Normalize(_appInfo.ExecutableDirectory);
        return installed is not null && running is not null
            && string.Equals(installed, running, StringComparison.OrdinalIgnoreCase)
            ? InstallType.Installed
            : InstallType.Portable;
    }

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(path.Trim().Trim('"'));
            var root = Path.GetPathRoot(full);
            return full == root ? full : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
