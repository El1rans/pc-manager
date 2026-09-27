using Porchlight.Core.Components;

namespace Porchlight.Core.RemoteSupport.Demo;

/// <summary>
/// DEBUG-only fake <see cref="IAnyDeskService"/> for the "demo data" mode (see
/// <c>Monitoring.Demo.DemoDataMode</c> and CONTRIBUTING.md's "Screenshots" section) - swapped in so
/// a documentation screenshot of the "Get help" page never shows the real machine's AnyDesk ID.
/// Reports a fixed, obviously-fake address ("demo" alias, so it renders the same way a real
/// alias would); never touches the real AnyDesk installation.
/// </summary>
internal sealed class DemoAnyDeskService : IAnyDeskService
{
    private static readonly AnyDeskState FakeState = new(
        IsInstalled: true,
        ExePath: @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe",
        Version: "8.0.0",
        Id: "123456789",
        Alias: "demo-family-pc",
        IsRunning: true,
        ComponentStatus: new ComponentStatus(
            ComponentState.Running,
            Version: "8.0.0",
            Path: @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe",
            PathIsTrusted: true));

    public Task<AnyDeskState> GetStateAsync(CancellationToken cancellationToken) => Task.FromResult(FakeState);

    public Task<AnyDeskState> LaunchAsync(CancellationToken cancellationToken) => Task.FromResult(FakeState);
}
