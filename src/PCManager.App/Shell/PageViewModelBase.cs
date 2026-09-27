using CommunityToolkit.Mvvm.ComponentModel;

namespace PCManager.App.Shell;

/// <summary>Common base for feature page view models; see <see cref="IPage"/>.</summary>
public abstract partial class PageViewModelBase : ObservableObject, IPage
{
    [ObservableProperty]
    private string? _badge;

    public abstract string Title { get; }

    public abstract string Glyph { get; }

    public abstract int Order { get; }

    public virtual bool IsPinnedToBottom => false;

    public virtual Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
