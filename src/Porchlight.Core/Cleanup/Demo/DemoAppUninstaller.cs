#if DEBUG
namespace Porchlight.Core.Cleanup.Demo;

/// <summary>DEBUG-only fake <see cref="IAppUninstaller"/> that launches nothing - see
/// <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoAppUninstaller : IAppUninstaller
{
    public bool CanStartUninstall(InstalledApp app) => true;

    public UninstallStartResult StartUninstall(InstalledApp app) => UninstallStartResult.Started;
}
#endif
