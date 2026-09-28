using System.Windows;

namespace Porchlight.App.Features.RemoteSupport;

/// <summary>Small OK/Cancel dialog for setting the optional helper name (see
/// docs/specs/06-remote-support.md's "should-fix": editing must be a deliberate action, not a
/// live-bound field a parent could misread mid-keystroke).</summary>
public partial class HelperNameDialog : Window
{
    public HelperNameDialog(string currentName)
    {
        InitializeComponent();
        NameTextBox.Text = currentName;
        NameTextBox.SelectAll();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    /// <summary>The name entered, trimmed. Only meaningful when this dialog's
    /// <see cref="Window.DialogResult"/> came back <see langword="true"/> (OK clicked).</summary>
    public string ResultName { get; private set; } = string.Empty;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultName = NameTextBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
