namespace Porchlight.App.Features.RemoteSupport;

/// <summary>
/// The clipboard, behind an interface so copy commands are unit-testable without touching the
/// real Windows clipboard (and so a transient clipboard failure - another app briefly holding it -
/// is handled the same way everywhere it is used).
/// </summary>
public interface IClipboardService
{
    /// <summary>Puts <paramref name="text"/> on the clipboard as plain text. Never throws - a
    /// failure (e.g. another app briefly holding the clipboard open) is logged and reported back as
    /// <see langword="false"/> instead, so the caller can tell the user to try again rather than
    /// claiming success.</summary>
    bool SetText(string text);

    /// <summary>The clipboard's current plain text, or null when it holds no text (or couldn't be
    /// read). Never throws - see <see cref="SetText"/>.</summary>
    string? GetText();
}
