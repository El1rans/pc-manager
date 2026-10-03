namespace Porchlight.Core.Safety;

/// <summary>A decoded <c>productState</c>.</summary>
/// <param name="State">On, off, paused or expired.</param>
/// <param name="DefinitionsUpToDate">True/false when reported; null when the value was not recognised.</param>
public sealed record ProductStateInfo(ProductRunState State, bool? DefinitionsUpToDate);
