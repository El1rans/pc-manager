using Porchlight.Core.Lighting;

namespace Porchlight.App.Tests.Features.Lighting;

internal sealed class FakeLightingConflictDetector : ILightingConflictDetector
{
    public IReadOnlyList<LightingConflictWarning> Warnings { get; set; } = [];

    public Task<IReadOnlyList<LightingConflictWarning>> DetectAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Warnings);
}
