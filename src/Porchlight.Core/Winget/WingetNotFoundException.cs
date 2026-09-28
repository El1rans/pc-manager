namespace Porchlight.Core.Winget;

/// <summary>Thrown when <c>winget.exe</c> could not be found on PATH. The message is meant to be
/// shown to the user as-is.</summary>
public sealed class WingetNotFoundException : Exception
{
    private const string DefaultMessage = "winget.exe was not found. Install \"App Installer\" from the Microsoft Store.";

    public WingetNotFoundException()
        : base(DefaultMessage)
    {
    }

    public WingetNotFoundException(string message)
        : base(message)
    {
    }

    public WingetNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
