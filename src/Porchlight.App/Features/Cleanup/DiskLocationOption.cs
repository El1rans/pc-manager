namespace Porchlight.App.Features.Cleanup;

/// <summary>A place the disk space map can look at: "Your files", a drive, or a chosen folder.</summary>
/// <param name="Label">Plain-language name shown in the picker and at the start of the breadcrumb.</param>
/// <param name="Path">Folder or drive root to scan.</param>
public sealed record DiskLocationOption(string Label, string Path);
