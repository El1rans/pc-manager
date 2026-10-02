namespace Porchlight.Core.WindowsServices;

/// <summary>A <see cref="ServiceChangeResult"/> plus, for <see cref="ServiceChangeResult.HasDependents"/>,
/// the display names of the running services that depend on the one being stopped.</summary>
public sealed record ServiceChangeOutcome(ServiceChangeResult Result, IReadOnlyList<string> Dependents)
{
    public static ServiceChangeOutcome Of(ServiceChangeResult result) => new(result, []);
}
