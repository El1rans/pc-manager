#if DEBUG
namespace Porchlight.Core.Lighting.Demo;

/// <summary>
/// DEBUG-only demo stand-in for <see cref="LightingConflictDetector"/>: reports no conflicts, so a
/// documentation screenshot never shows which lighting software (G HUB, Armoury Crate, ...) is
/// really running on the machine taking it. See <see cref="Monitoring.Demo.DemoDataMode"/>.
/// </summary>
public sealed class DemoLightingConflictDetector : ILightingConflictDetector
{
    public Task<IReadOnlyList<LightingConflictWarning>> DetectAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LightingConflictWarning>>([]);
}
#endif
