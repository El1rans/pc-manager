namespace Porchlight.Core.Winget;

/// <summary>One app listed in a <c>winget export</c> file.</summary>
/// <param name="Id">The winget package identifier (e.g. <c>VideoLAN.VLC</c>).</param>
public sealed record WingetExportApp(string Id);
