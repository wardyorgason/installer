using Installer.Dao;
using Installer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Installer.Cli;

/// <summary>The composition root: the only CLI type that may reference Installer.Dao.</summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddInstaller(this IServiceCollection services, CliOptions options)
    {
        services.TryAddTimeProvider();
        return services
            .AddInstallerDao()
            .AddInstallerServices()
            .AddInstallerCli(options);
    }

    public static ServiceProvider BuildProvider(CliOptions options) =>
        new ServiceCollection()
            .AddInstaller(options)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
