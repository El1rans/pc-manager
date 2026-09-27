namespace PCManager.App.Features.RemoteSupport;

/// <summary>
/// The clipboard, behind an interface so copy commands are unit-testable without touching the
/// real Windows clipboard (and so a transient clipboard failure - another app briefly holding it -
/// is handled the same way everywhere it is used).
/// </summary>
public interface IClipboardService
{
    /// <summary>Puts <paramref name="text"/> on the clipboard as plain text. Never throws; a
    /// failure is logged and otherwise ignored, since a copy button is not worth crashing over.</summary>
    void SetText(string text);
}
