using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.App.Shell;

public static class PageServiceCollectionExtensions
{
    /// <summary>
    /// Registers a feature page: its view model as a singleton (also exposed as <see cref="IPage"/>
    /// so the navigation rail picks it up), and its view mapping for the
    /// <see cref="IPageViewLocator"/>. This is the only shell-facing registration a feature needs -
    /// it never touches <c>App.xaml</c> or any other feature's files.
    /// <para>The container tracks the instance under both registrations - the forwarding
    /// <see cref="IPage"/> factory's result is captured for disposal just like the concrete
    /// singleton - so an <see cref="IDisposable"/> page view model is disposed TWICE on host
    /// shutdown. Every page view model's <c>Dispose</c> must therefore be idempotent (guard it with a
    /// <c>_disposed</c> flag); anything it created itself (e.g. a <c>ComponentCardViewModel</c> from
    /// <c>IComponentCardViewModelFactory</c>, which the container never sees) it disposes itself.</para>
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
