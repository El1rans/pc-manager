namespace Porchlight.Core.Startup;

/// <summary>A string value found in a <c>Run</c> key: its name and the command line it holds.</summary>
public sealed record StartupRunValue(string Name, string Command);
