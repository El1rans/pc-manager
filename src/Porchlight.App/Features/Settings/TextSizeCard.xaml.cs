using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.App.Features.Settings;

/// <summary>The "Text size" card. It is created by XAML, so it resolves its view model from the
/// app's container (the same way <c>AdminRequiredBanner</c> reaches shared services).</summary>
public partial class TextSizeCard : UserControl
{
    public TextSizeCard()
    {
        InitializeComponent();
        if (App.Services?.GetService<TextSizeViewModel>() is { } viewModel)
        {
            DataContext = viewModel;
        }
    }
}
