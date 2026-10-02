namespace Porchlight.Core.Winget;

/// <summary>The curated list the Get apps page shows before the user searches. Every id was checked
/// with <c>winget show --id &lt;id&gt; --exact --source winget</c>; never add a Microsoft Store id
/// (<c>9N...</c>) - only the community <c>winget</c> source is used.</summary>
public static class PopularApps
{
    // WhatsApp is deliberately absent: it is only published in the Microsoft Store, not in the
    // winget community source (spec 27: "only if a non-Store winget id exists").
    public static IReadOnlyList<WingetSearchResult> All { get; } =
    [
        new("Google Chrome", "Google.Chrome", string.Empty),
        new("Mozilla Firefox", "Mozilla.Firefox", string.Empty),
        new("VLC media player", "VideoLAN.VLC", string.Empty),
        new("7-Zip", "7zip.7zip", string.Empty),
        new("Zoom", "Zoom.Zoom", string.Empty),
        new("Spotify", "Spotify.Spotify", string.Empty),
        new("Adobe Acrobat Reader", "Adobe.Acrobat.Reader.64-bit", string.Empty),
        new("Notepad++", "Notepad++.Notepad++", string.Empty),
        new("Discord", "Discord.Discord", string.Empty),
    ];
}
