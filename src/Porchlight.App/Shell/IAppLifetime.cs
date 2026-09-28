namespace Porchlight.App.Shell;

/// <summary>Abstraction over shutting the WPF application down, so view models never need to
/// reference <see cref="System.Windows.Application"/> directly.</summary>
public interface IAppLifetime
{
    /// <summary>Begins an orderly shutdown of the application.</summary>
    void Shutdown(int exitCode = 0);
}
