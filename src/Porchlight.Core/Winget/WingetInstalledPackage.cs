namespace Porchlight.Core.Winget;

/// <summary>One row of <c>winget list --source winget</c>.</summary>
/// <param name="Name">Name as winget shows it (may end with an ellipsis when winget shortened it).</param>
/// <param name="Id">Exact winget package id.</param>
/// <param name="Version">Installed version as winget shows it.</param>
public sealed record WingetInstalledPackage(string Name, string Id, string Version);
