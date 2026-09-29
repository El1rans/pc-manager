namespace Porchlight.Core.Network;

/// <summary>A program and how many active internet connections it has.</summary>
/// <param name="Name">Friendly program name.</param>
/// <param name="ConnectionCount">Number of established TCP connections.</param>
public sealed record NetworkAppUsage(string Name, int ConnectionCount);
