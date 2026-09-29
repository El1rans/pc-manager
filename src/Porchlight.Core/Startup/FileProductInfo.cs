namespace Porchlight.Core.Startup;

/// <summary>Version-resource text of an executable (any part may be null or empty).</summary>
public sealed record FileProductInfo(string? Description, string? Product, string? Company);
