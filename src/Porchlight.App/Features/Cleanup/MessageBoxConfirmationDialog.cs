using System.Windows;

namespace Porchlight.App.Features.Cleanup;

/// <inheritdoc cref="IConfirmationDialog"/>
public sealed class MessageBoxConfirmationDialog : IConfirmationDialog
{
    public bool Confirm(string title, string message)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var result = owner is { IsVisible: true }
            ? MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }
}
