namespace Porchlight.App.Tray;

/// <summary>One entry of the tray icon's right-click menu. A null <paramref name="Invoke"/> makes
/// it a separator.</summary>
public sealed record TrayMenuItem(string Text, Action? Invoke)
{
    public static TrayMenuItem Separator { get; } = new(string.Empty, null);

    public bool IsSeparator => Invoke is null;
}
