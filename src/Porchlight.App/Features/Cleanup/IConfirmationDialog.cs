namespace Porchlight.App.Features.Cleanup;

/// <summary>Asks the user a yes/no question before something irreversible or surprising, behind an
/// interface so view model tests never show a real window.</summary>
public interface IConfirmationDialog
{
    /// <summary>Shows <paramref name="message"/> and returns true only if the user chose Yes. The
    /// default button is No.</summary>
    bool Confirm(string title, string message);
}
