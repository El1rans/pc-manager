using System.Windows;
using Porchlight.App.Shell;

namespace Porchlight.App.Features.Notifications;

/// <summary>The small "Notifications" settings dialog. See <see cref="NotificationsViewModel"/>.</summary>
public partial class NotificationsWindow : Window
{
    public NotificationsWindow(NotificationsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // See WhiteFlashGuard: without this, the freshly shown window can paint solid white until
        // the user clicks it.
        WhiteFlashGuard.Attach(this);

        // The sign-in checkbox shows the real scheduled-task state, read once the dialog opens.
        Loaded += async (_, _) => await viewModel.LoadStartAtLoginAsync().ConfigureAwait(true);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
