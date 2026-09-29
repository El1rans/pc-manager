using Porchlight.App.Features.Lighting;

namespace Porchlight.App.Tests.Features.Lighting;

internal sealed class FakeAnimationFilePicker : IAnimationFilePicker
{
    /// <summary>What <see cref="PickFile"/> returns; null simulates the user cancelling.</summary>
    public string? NextPath { get; set; }

    public string? PickFile() => NextPath;
}
