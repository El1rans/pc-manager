using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace PCManager.App.Shell;

public static class PageServiceCollectionExtensions
{
    /// <summary>
    /// Registers a feature page: its view model as a singleton (also exposed as <see cref="IPage"/>
    /// so the navigation rail picks it up), and its view mapping for the
    /// <see cref="IPageViewLocator"/>. This is the only shell-facing registration a feature needs -
    /// it never touches <c>App.xaml</c> or any other feature's files.
    /// </summary>
    public static IServiceCollection AddPage<TViewModel, TView>(this IServiceCollection services)
        where TViewModel : class, IPage
        where TView : FrameworkElement, new()
    {
        services.AddSingleton<TViewModel>();
        services.AddSingleton<IPage>(sp => sp.GetRequiredService<TViewModel>());
        services.AddSingleton(new PageRegistration(typeof(TViewModel), static () => new TView()));
        return services;
    }
}
