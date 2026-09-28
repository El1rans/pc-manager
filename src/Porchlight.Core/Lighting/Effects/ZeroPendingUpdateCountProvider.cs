namespace Porchlight.Core.Lighting.Effects;

/// <summary>Default <see cref="IPendingUpdateCountProvider"/>: always reports zero. Registered
/// until the Updates feature supplies a real implementation.</summary>
public sealed class ZeroPendingUpdateCountProvider : IPendingUpdateCountProvider
{
    public int GetPendingUpdateCount() => 0;
}
