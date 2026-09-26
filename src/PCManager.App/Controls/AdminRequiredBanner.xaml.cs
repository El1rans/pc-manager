using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCManager.App.Controls;

/// <summary>
/// Banner shown by feature pages that need administrator rights to show everything. Pair with
/// each page's own check of <c>IElevationService.IsElevated</c> to control its visibility.
/// </summary>
public partial class AdminRequiredBanner : UserControl
{
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(AdminRequiredBanner),
        new PropertyMetadata("This page needs administrator rights to show everything."));

    public static readonly DependencyProperty RestartCommandProperty = DependencyProperty.Register(
        nameof(RestartCommand), typeof(ICommand), typeof(AdminRequiredBanner));

    public AdminRequiredBanner()
    {
        InitializeComponent();
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public ICommand? RestartCommand
    {
        get => (ICommand?)GetValue(RestartCommandProperty);
        set => SetValue(RestartCommandProperty, value);
    }
}
