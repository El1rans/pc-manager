namespace Porchlight.App.Features.Dashboard;

/// <summary>One label/value row of the "System" card.</summary>
/// <param name="Label">e.g. <c>Computer name</c>.</param>
/// <param name="Value">e.g. <c>DESKTOP-ABC123</c>.</param>
public sealed record SystemInfoRowViewModel(string Label, string Value);
