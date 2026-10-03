using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Porchlight.Core.Checkup.Sections;

namespace Porchlight.Core.Checkup;

/// <summary>Registers the check-up report builder and its built-in sections. Other features add their
/// own section with <c>services.AddSingleton&lt;ICheckupSection, MySection&gt;()</c>. Relies on the
/// monitoring, hardware, winget and remote-support services being registered by their own features.</summary>
public static class CheckupServiceCollectionExtensions
{
    public static IServiceCollection AddCheckup(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ICheckupReportBuilder, CheckupReportBuilder>();
        services.AddSingleton<CheckupReminderScheduler>();

        services.AddSingleton<ICheckupSection, ComputerCheckupSection>();
        services.AddSingleton<ICheckupSection, RestartCheckupSection>();
        services.AddSingleton<ICheckupSection, DriveSpaceCheckupSection>();
        services.AddSingleton<ICheckupSection, BackupCheckupSection>();
        services.AddSingleton<ICheckupSection, AppUpdatesCheckupSection>();
        services.AddSingleton<ICheckupSection, TemperatureCheckupSection>();
        services.AddSingleton<ICheckupSection, RemoteHelpCheckupSection>();
        return services;
    }
}
