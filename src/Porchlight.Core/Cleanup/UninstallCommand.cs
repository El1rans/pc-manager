namespace Porchlight.Core.Cleanup;

/// <summary>An uninstall command line split into the program and its arguments.</summary>
public sealed record UninstallCommand(string FileName, IReadOnlyList<string> Arguments);
