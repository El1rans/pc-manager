namespace Porchlight.Core.Startup;

/// <summary>A file in a Startup folder. <paramref name="TargetPath"/> is the resolved shortcut
/// target, or null when it is not a shortcut or could not be resolved.</summary>
public sealed record StartupFolderItem(string FileName, string? TargetPath);
