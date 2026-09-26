using System.Windows;

namespace PCManager.App.Shell;

/// <summary>
/// Records that a page's view model type is rendered by a particular view factory. Produced by
/// <see cref="PageServiceCollectionExtensions.AddPage{TViewModel, TView}"/> and applied to the
/// <see cref="IPageViewLocator"/> once the DI container is built.
/// </summary>
public sealed record PageRegistration(Type ViewModelType, Func<FrameworkElement> CreateView);
